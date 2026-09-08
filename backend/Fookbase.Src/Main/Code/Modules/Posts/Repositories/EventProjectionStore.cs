using Fookbase.Api.Shared.Contracts.Friends;
using Fookbase.Api.Shared.Contracts.Identity;
using Fookbase.Api.Shared.Contracts.Media;
using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Repositories;

public sealed class EventProjectionStore(
    PostsDbContext dbContext,
    TimeProvider timeProvider)
{
    public Task<bool> ProjectAsync(
        UserRegisteredIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        ProjectOnceAsync(
            integrationEvent.EventId,
            UserRegisteredIntegrationEvent.EventType,
            async () =>
            {
                if (!await dbContext.KnownUsers.AnyAsync(
                        user => user.UserId == integrationEvent.UserId,
                        cancellationToken))
                {
                    dbContext.KnownUsers.Add(KnownUser.Create(
                        integrationEvent.UserId,
                        integrationEvent.OccurredAtUtc));
                }
            },
            cancellationToken);

    public Task<bool> ProjectAsync(
        FriendRequestAcceptedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        ProjectFriendshipAsync(
            integrationEvent.EventId,
            FriendRequestAcceptedIntegrationEvent.EventType,
            integrationEvent.UserId1,
            integrationEvent.UserId2,
            isActive: true,
            integrationEvent.OccurredAtUtc,
            cancellationToken);

    public Task<bool> ProjectAsync(
        FriendshipRemovedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        ProjectFriendshipAsync(
            integrationEvent.EventId,
            FriendshipRemovedIntegrationEvent.EventType,
            integrationEvent.UserId1,
            integrationEvent.UserId2,
            isActive: false,
            integrationEvent.OccurredAtUtc,
            cancellationToken);

    public Task<bool> ProjectAsync(
        UserBlockedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        ProjectBlockAsync(
            integrationEvent.EventId,
            UserBlockedIntegrationEvent.EventType,
            integrationEvent.BlockerUserId,
            integrationEvent.BlockedUserId,
            isActive: true,
            integrationEvent.OccurredAtUtc,
            cancellationToken);

    public Task<bool> ProjectAsync(
        UserUnblockedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        ProjectBlockAsync(
            integrationEvent.EventId,
            UserUnblockedIntegrationEvent.EventType,
            integrationEvent.BlockerUserId,
            integrationEvent.BlockedUserId,
            isActive: false,
            integrationEvent.OccurredAtUtc,
            cancellationToken);

    public Task<bool> ProjectAsync(MediaReadyIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) => ProjectOnceAsync(
        integrationEvent.EventId, MediaReadyIntegrationEvent.EventType, async () =>
        {
            var media = await dbContext.KnownMedia.SingleOrDefaultAsync(
                x => x.MediaId == integrationEvent.MediaId, cancellationToken);
            if (media is null)
                dbContext.KnownMedia.Add(KnownMedia.Create(integrationEvent.MediaId,
                    integrationEvent.OwnerUserId, integrationEvent.MediaType, integrationEvent.ContentType,
                    integrationEvent.SizeBytes, integrationEvent.OccurredAtUtc));
            else
                media.MarkReady(integrationEvent.OwnerUserId, integrationEvent.MediaType,
                    integrationEvent.ContentType, integrationEvent.SizeBytes, integrationEvent.OccurredAtUtc);
        }, cancellationToken);

    public Task<bool> ProjectAsync(MediaDeletedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) => ProjectOnceAsync(
        integrationEvent.EventId, MediaDeletedIntegrationEvent.EventType, async () =>
        {
            var media = await dbContext.KnownMedia.SingleOrDefaultAsync(
                x => x.MediaId == integrationEvent.MediaId, cancellationToken);
            if (media is null)
                dbContext.KnownMedia.Add(KnownMedia.CreateDeleted(integrationEvent.MediaId,
                    integrationEvent.OwnerUserId, integrationEvent.OccurredAtUtc));
            else media.MarkDeleted(integrationEvent.OccurredAtUtc);
        }, cancellationToken);

    private Task<bool> ProjectFriendshipAsync(
        Guid eventId,
        string eventType,
        Guid firstUserId,
        Guid secondUserId,
        bool isActive,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var pair = UserPair.Create(firstUserId, secondUserId);
        return ProjectOnceAsync(
            eventId,
            eventType,
            () => dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
                INSERT INTO "FriendEdges"
                    ("UserId1", "UserId2", "CreatedAtUtc", "LastChangedAtUtc", "IsActive")
                VALUES
                    ({{pair.UserId1}}, {{pair.UserId2}}, {{occurredAtUtc}}, {{occurredAtUtc}}, {{isActive}})
                ON CONFLICT ("UserId1", "UserId2") DO UPDATE SET
                    "CreatedAtUtc" = EXCLUDED."CreatedAtUtc",
                    "LastChangedAtUtc" = EXCLUDED."LastChangedAtUtc",
                    "IsActive" = EXCLUDED."IsActive"
                WHERE EXCLUDED."LastChangedAtUtc" >= "FriendEdges"."LastChangedAtUtc";
                """, cancellationToken),
            cancellationToken);
    }

    private Task<bool> ProjectBlockAsync(
        Guid eventId,
        string eventType,
        Guid blockerUserId,
        Guid blockedUserId,
        bool isActive,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken) =>
        ProjectOnceAsync(
            eventId,
            eventType,
            () => dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
                INSERT INTO "BlockedEdges"
                    ("BlockerUserId", "BlockedUserId", "CreatedAtUtc", "LastChangedAtUtc", "IsActive")
                VALUES
                    ({{blockerUserId}}, {{blockedUserId}}, {{occurredAtUtc}}, {{occurredAtUtc}}, {{isActive}})
                ON CONFLICT ("BlockerUserId", "BlockedUserId") DO UPDATE SET
                    "CreatedAtUtc" = EXCLUDED."CreatedAtUtc",
                    "LastChangedAtUtc" = EXCLUDED."LastChangedAtUtc",
                    "IsActive" = EXCLUDED."IsActive"
                WHERE EXCLUDED."LastChangedAtUtc" >= "BlockedEdges"."LastChangedAtUtc";
                """, cancellationToken),
            cancellationToken);

    private async Task<bool> ProjectOnceAsync(
        Guid eventId,
        string eventType,
        Func<Task> project,
        CancellationToken cancellationToken)
    {
        if (await dbContext.InboxMessages.AnyAsync(
                message => message.EventId == eventId,
                cancellationToken))
        {
            return false;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({eventId.ToString("N")}, 2))",
            cancellationToken);
        if (await dbContext.InboxMessages.AnyAsync(
                message => message.EventId == eventId,
                cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await project();
        dbContext.InboxMessages.Add(InboxMessage.Create(eventId, eventType, timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
