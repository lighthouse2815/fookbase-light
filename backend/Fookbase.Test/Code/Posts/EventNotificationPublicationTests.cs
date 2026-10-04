using Fookbase.Api.Modules.Events.Domain.Enums;
using Fookbase.Api.Modules.Events.DTOs.Requests;
using Fookbase.Api.Modules.Events.Entities;
using Fookbase.Api.Modules.Events.Services;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Notifications.Hubs;
using Fookbase.Api.Modules.Notifications.Services;
using Fookbase.Api.Modules.Users.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class EventNotificationApiFactory : PostsApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHubContext<NotificationsHub>>();
            services.AddSingleton<RecordingNotificationHubContext>();
            services.AddSingleton<IHubContext<NotificationsHub>>(provider =>
                provider.GetRequiredService<RecordingNotificationHubContext>());
        });
    }
}

public sealed class EventNotificationPublicationTests(EventNotificationApiFactory factory) : IClassFixture<EventNotificationApiFactory>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Event_changes_publish_persisted_unread_notifications_once_to_each_other_participant(bool cancel)
    {
        var seeded = await SeedEventAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var events = scope.ServiceProvider.GetRequiredService<EventsService>();
            var result = cancel
                ? await events.CancelAsync(seeded.OwnerId, seeded.Event.Id)
                : await events.UpdateAsync(seeded.OwnerId, seeded.Event.Id, UpdateRequest(seeded.Event, "Da Nang"));
            Assert.True(result.Succeeded);
        }

        var recorder = factory.Services.GetRequiredService<RecordingNotificationHubContext>();
        var deliveries = recorder.Deliveries.Where(item => item.Response.EntityId == seeded.Event.Id).ToArray();
        Assert.Equal(2, deliveries.Length);
        Assert.Equal(new[] { seeded.GoingId, seeded.InterestedId }.Order(),
            deliveries.Select(item => Guid.Parse(item.RecipientUserId)).Order());
        Assert.All(deliveries, delivery =>
        {
            Assert.Equal("NotificationReceived", delivery.Method);
            Assert.True(delivery.WasPersisted);
            Assert.Equal(cancel ? EventStatus.CANCELLED : EventStatus.PUBLISHED, delivery.PersistedEventStatus);
            Assert.Equal(cancel ? "Hanoi" : "Da Nang", delivery.PersistedEventLocationName);
            Assert.Equal(cancel ? "EventCancelled" : "EventUpdated", delivery.Response.Type);
            Assert.Equal("Event", delivery.Response.EntityType);
            Assert.Equal(seeded.Event.Id, delivery.Response.EntityId);
            Assert.Null(delivery.Response.ParentEntityId);
            Assert.Equal(seeded.OwnerId, delivery.Response.ActorUserId);
            Assert.Equal(Guid.Parse(delivery.RecipientUserId), delivery.Response.RecipientUserId);
            Assert.False(delivery.Response.IsRead);
            Assert.Null(delivery.Response.ReadAtUtc);
        });

        using var verificationScope = factory.Services.CreateScope();
        var db = verificationScope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Equal(2, await db.Notifications.CountAsync(item => item.EntityId == seeded.Event.Id));
        Assert.False(await db.Notifications.AnyAsync(item => item.EntityId == seeded.Event.Id &&
            (item.RecipientUserId == seeded.OwnerId || item.RecipientUserId == seeded.StrangerId)));

        var notifications = verificationScope.ServiceProvider.GetRequiredService<NotificationService>();
        var goingDelivery = Assert.Single(deliveries, item => item.Response.RecipientUserId == seeded.GoingId);
        var page = await notifications.GetNotificationsAsync(seeded.GoingId, null, 20);
        var persistedResponse = Assert.Single(page.Items);
        // PostgreSQL stores timestamps at microsecond precision; the queued entity retains its original ticks.
        Assert.Equal(goingDelivery.Response with { CreatedAtUtc = persistedResponse.CreatedAtUtc }, persistedResponse);
        Assert.InRange(Math.Abs((goingDelivery.Response.CreatedAtUtc - persistedResponse.CreatedAtUtc).Ticks), 0L, 9L);
        Assert.Equal(1, await notifications.GetUnreadCountAsync(seeded.GoingId));
        Assert.True(await notifications.MarkReadAsync(seeded.GoingId, goingDelivery.Response.Id));
        Assert.True(await notifications.MarkReadAsync(seeded.GoingId, goingDelivery.Response.Id));
        Assert.Equal(0, await notifications.GetUnreadCountAsync(seeded.GoingId));
        var read = Assert.Single((await notifications.GetNotificationsAsync(seeded.GoingId, null, 20)).Items);
        Assert.True(read.IsRead);
        Assert.NotNull(read.ReadAtUtc);
        Assert.Equal(2, recorder.Deliveries.Count(item => item.Response.EntityId == seeded.Event.Id));
    }

    [Fact]
    public async Task Unchanged_invalid_and_forbidden_updates_do_not_queue_or_publish_notifications()
    {
        var seeded = await SeedEventAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var events = scope.ServiceProvider.GetRequiredService<EventsService>();
            Assert.True((await events.UpdateAsync(seeded.OwnerId, seeded.Event.Id, UpdateRequest(seeded.Event, "Hanoi"))).Succeeded);
            Assert.True((await events.UpdateAsync(seeded.OwnerId, seeded.Event.Id,
                UpdateRequest(seeded.Event, "  Hanoi  ") with { Address = "", OnlineUrl = "https://ignored.example.com" })).Succeeded);
        }
        using (var scope = factory.Services.CreateScope())
        {
            var events = scope.ServiceProvider.GetRequiredService<EventsService>();
            Assert.False((await events.UpdateAsync(seeded.OwnerId, seeded.Event.Id,
                UpdateRequest(seeded.Event, "Da Nang") with { EndsAtUtc = seeded.Event.StartsAtUtc.AddMinutes(-1) })).Succeeded);
        }
        using (var scope = factory.Services.CreateScope())
        {
            var events = scope.ServiceProvider.GetRequiredService<EventsService>();
            Assert.False((await events.UpdateAsync(seeded.StrangerId, seeded.Event.Id, UpdateRequest(seeded.Event, "Da Nang"))).Succeeded);
        }

        using var verificationScope = factory.Services.CreateScope();
        var db = verificationScope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.False(await db.Notifications.AnyAsync(item => item.EntityId == seeded.Event.Id));
        Assert.DoesNotContain(factory.Services.GetRequiredService<RecordingNotificationHubContext>().Deliveries,
            item => item.Response.EntityId == seeded.Event.Id);
        Assert.Equal("Hanoi", (await db.Events.AsNoTracking().SingleAsync(item => item.Id == seeded.Event.Id)).LocationName);
    }

    [Fact]
    public async Task A_repeated_cancellation_does_not_publish_another_notification()
    {
        var seeded = await SeedEventAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var events = scope.ServiceProvider.GetRequiredService<EventsService>();
            Assert.True((await events.CancelAsync(seeded.OwnerId, seeded.Event.Id)).Succeeded);
        }
        using (var scope = factory.Services.CreateScope())
        {
            var events = scope.ServiceProvider.GetRequiredService<EventsService>();
            Assert.False((await events.CancelAsync(seeded.OwnerId, seeded.Event.Id)).Succeeded);
        }

        using var verificationScope = factory.Services.CreateScope();
        var db = verificationScope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Equal(2, await db.Notifications.CountAsync(item => item.EntityId == seeded.Event.Id));
        Assert.Equal(2, factory.Services.GetRequiredService<RecordingNotificationHubContext>().Deliveries
            .Count(item => item.Response.EntityId == seeded.Event.Id));
    }

    private static UpdateEventRequest UpdateRequest(Event item, string locationName) => new(
        item.Name, item.Description, "public", "physical", locationName, item.Address, null,
        item.StartsAtUtc, item.EndsAtUtc);

    private async Task<SeededEvent> SeedEventAsync()
    {
        var now = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var users = Enumerable.Range(0, 4).Select(index => new User(Guid.NewGuid(),
            $"event-publication-{Guid.NewGuid():N}@example.com", $"event_notify_{Guid.NewGuid():N}"[..32], now.AddTicks(index))).ToArray();
        var item = Event.Create(Guid.NewGuid(), "Notification event", null, EventHostType.USER, users[0].Id,
            users[0].Id, EventPrivacy.PUBLIC, EventLocationType.PHYSICAL, "Hanoi", null, null,
            now.AddDays(7), null, EventStatus.PUBLISHED, now);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.Users.AddRange(users);
        db.UserProfiles.AddRange(users.Select(user => new UserProfile(user.Id, user.UserName!, now)));
        db.Events.Add(item);
        db.EventParticipants.AddRange(
            EventParticipant.Create(item.Id, users[0].Id, EventParticipantStatus.GOING, now),
            EventParticipant.Create(item.Id, users[1].Id, EventParticipantStatus.GOING, now),
            EventParticipant.Create(item.Id, users[2].Id, EventParticipantStatus.INTERESTED, now));
        await db.SaveChangesAsync();
        return new(item, users[0].Id, users[1].Id, users[2].Id, users[3].Id);
    }

    private sealed record SeededEvent(Event Event, Guid OwnerId, Guid GoingId, Guid InterestedId, Guid StrangerId);
}
