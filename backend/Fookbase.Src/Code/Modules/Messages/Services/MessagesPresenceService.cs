using System.Collections.Concurrent;
using Fookbase.Api.Modules.Messages.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Messages.Services;

/// <summary>
/// In-memory presence for one API instance. Scaling SignalR/presence across instances
/// needs a shared backplane and is intentionally deferred.
/// </summary>
public sealed class MessagesPresenceService(
    IServiceScopeFactory scopeFactory,
    IHubContext<MessagesHub> hubContext)
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> connections = [];

    public async Task ConnectedAsync(Guid userId, string connectionId, CancellationToken cancellationToken)
    {
        var userConnections = connections.GetOrAdd(userId, _ => []);
        if (userConnections.TryAdd(connectionId, 0) && userConnections.Count == 1)
        {
            await BroadcastAsync(userId, true, cancellationToken);
        }
    }

    public async Task DisconnectedAsync(Guid userId, string connectionId, CancellationToken cancellationToken)
    {
        if (!connections.TryGetValue(userId, out var userConnections))
        {
            return;
        }

        userConnections.TryRemove(connectionId, out _);
        if (!userConnections.IsEmpty || !connections.TryRemove(userId, out _))
        {
            return;
        }

        await BroadcastAsync(userId, false, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetVisibleOnlineUserIdsAsync(Guid viewerUserId, CancellationToken cancellationToken)
    {
        var onlineUserIds = connections
            .Where(entry => !entry.Value.IsEmpty && entry.Key != viewerUserId)
            .Select(entry => entry.Key)
            .ToArray();
        if (onlineUserIds.Length == 0)
        {
            return [];
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var conversationIds = await dbContext.ConversationParticipants.AsNoTracking()
            .Where(participant => participant.UserId == viewerUserId && participant.LeftAtUtc == null)
            .Select(participant => participant.ConversationId)
            .ToArrayAsync(cancellationToken);
        if (conversationIds.Length == 0)
        {
            return [];
        }

        var blockedUserIds = await dbContext.BlockedUsers.AsNoTracking()
            .Where(block => block.BlockerUserId == viewerUserId || block.BlockedUserId == viewerUserId)
            .Select(block => block.BlockerUserId == viewerUserId ? block.BlockedUserId : block.BlockerUserId)
            .ToArrayAsync(cancellationToken);
        return await dbContext.ConversationParticipants.AsNoTracking()
            .Where(participant => conversationIds.Contains(participant.ConversationId) &&
                                  participant.LeftAtUtc == null &&
                                  onlineUserIds.Contains(participant.UserId) &&
                                  !blockedUserIds.Contains(participant.UserId))
            .Select(participant => participant.UserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    private async Task BroadcastAsync(Guid subjectUserId, bool isOnline, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var conversationIds = await dbContext.ConversationParticipants.AsNoTracking()
            .Where(participant => participant.UserId == subjectUserId && participant.LeftAtUtc == null)
            .Select(participant => participant.ConversationId)
            .ToArrayAsync(cancellationToken);
        if (conversationIds.Length == 0)
        {
            return;
        }

        var blockedUserIds = await dbContext.BlockedUsers.AsNoTracking()
            .Where(block => block.BlockerUserId == subjectUserId || block.BlockedUserId == subjectUserId)
            .Select(block => block.BlockerUserId == subjectUserId ? block.BlockedUserId : block.BlockerUserId)
            .ToArrayAsync(cancellationToken);
        var recipients = await dbContext.ConversationParticipants.AsNoTracking()
            .Where(participant => conversationIds.Contains(participant.ConversationId) &&
                                  participant.LeftAtUtc == null && participant.UserId != subjectUserId &&
                                  !blockedUserIds.Contains(participant.UserId))
            .Select(participant => participant.UserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        if (recipients.Length > 0)
        {
            await hubContext.Clients.Users(recipients.Select(userId => userId.ToString())).SendAsync(
                "PresenceChanged",
                new { UserId = subjectUserId, IsOnline = isOnline },
                cancellationToken);
        }
    }
}
