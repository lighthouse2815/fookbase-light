using Fookbase.Api.Modules.Events.Domain.Enums;
using System.Collections.Concurrent;
using Fookbase.Api.Modules.Events.Entities;
using Fookbase.Api.Modules.Notifications.DTOs.Responses;
using Fookbase.Api.Modules.Notifications.Entities;
using Fookbase.Api.Modules.Notifications.Hubs;
using Fookbase.Api.Modules.Notifications.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Posts.Api.IntegrationTests;

internal sealed record RecordedNotificationDelivery(
    string RecipientUserId,
    string Method,
    NotificationResponse Response,
    bool WasPersisted,
    EventStatus? PersistedEventStatus,
    string? PersistedEventLocationName);

internal sealed class RecordingNotificationHubContext(IServiceScopeFactory scopeFactory) : IHubContext<NotificationsHub>
{
    public ConcurrentQueue<RecordedNotificationDelivery> Deliveries { get; } = new();
    public IHubClients Clients => new RecordingHubClients(this);
    public IGroupManager Groups => throw new NotSupportedException();

    public static async Task AssertPublishedResponseAsync(IServiceProvider services, NotificationResponse expected)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var notification = await db.Notifications.AsNoTracking().SingleAsync(item => item.Id == expected.Id);
        var recorder = new RecordingNotificationHubContext(services.GetRequiredService<IServiceScopeFactory>());
        var publisher = new NotificationService(db, recorder, services.GetRequiredService<TimeProvider>());
        await publisher.PublishAsync(notification);

        var delivery = Assert.Single(recorder.Deliveries);
        Assert.Equal(expected.RecipientUserId.ToString(), delivery.RecipientUserId);
        Assert.Equal("NotificationReceived", delivery.Method);
        Assert.True(delivery.WasPersisted);
        Assert.Equal(expected, delivery.Response);
    }

    private async Task RecordAsync(string recipientUserId, string method, object?[] args, CancellationToken cancellationToken)
    {
        var response = (NotificationResponse)args.Single()!;
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var persisted = await db.Notifications.AsNoTracking().AnyAsync(item =>
            item.Id == response.Id && item.RecipientUserId == response.RecipientUserId &&
            item.ActorUserId == response.ActorUserId && item.IsRead == response.IsRead &&
            item.ReadAtUtc == response.ReadAtUtc && item.EntityId == response.EntityId, cancellationToken);
        var eventId = response.Type == "EventInvite" ? response.ParentEntityId : response.EntityId;
        var itemEvent = response.EntityType == "Event"
            ? await db.Events.AsNoTracking().SingleOrDefaultAsync(item => item.Id == eventId, cancellationToken)
            : null;
        Deliveries.Enqueue(new(recipientUserId, method, response, persisted, itemEvent?.Status, itemEvent?.LocationName));
    }

    private sealed class RecordingClientProxy(RecordingNotificationHubContext owner, string recipientUserId) : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) =>
            owner.RecordAsync(recipientUserId, method, args, cancellationToken);
    }

    private sealed class RecordingHubClients(RecordingNotificationHubContext owner) : IHubClients
    {
        public IClientProxy All => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Client(string connectionId) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IClientProxy Group(string groupName) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        public IClientProxy User(string userId) => new RecordingClientProxy(owner, userId);
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }
}
