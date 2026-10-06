using Fookbase.Api.Modules.Events.Domain.Enums;
using Fookbase.Api.Modules.Groups.Domain.Enums;
using Fookbase.Api.Modules.Events.Entities;
using Fookbase.Api.Modules.Events.Services;
using Fookbase.Api.Modules.Friends.Entities;
using Fookbase.Api.Modules.Groups.Entities;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Pages.Domain.Enums;
using Fookbase.Api.Modules.Pages.Entities;
using Fookbase.Api.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class CancelledEventAccessTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    [Theory]
    [InlineData(EventPrivacy.PUBLIC)]
    [InlineData(EventPrivacy.PRIVATE)]
    public async Task Cancelled_event_details_remain_available_only_to_existing_participants_and_pending_invitees(EventPrivacy privacy)
    {
        var seeded = await SeedAsync(privacy);
        using var scope = factory.Services.CreateScope();
        var events = scope.ServiceProvider.GetRequiredService<EventsService>();
        var access = scope.ServiceProvider.GetRequiredService<EventAccessService>();

        Assert.True((await events.GetAsync(seeded.Event.Id, seeded.ParticipantId)).Succeeded);
        Assert.True((await events.GetAsync(seeded.Event.Id, seeded.InviteeId)).Succeeded);
        Assert.False((await events.GetAsync(seeded.Event.Id, seeded.DeclinedId)).Succeeded);
        Assert.False((await events.GetAsync(seeded.Event.Id, seeded.StrangerId)).Succeeded);
        Assert.False((await events.GetAsync(seeded.Event.Id, null)).Succeeded);
        Assert.False(await access.CanPostAsync(seeded.Event, seeded.ParticipantId));
    }

    [Theory]
    [InlineData(EventStatus.DRAFT)]
    [InlineData(EventStatus.PUBLISHED)]
    public async Task Existing_draft_and_published_event_visibility_does_not_change(EventStatus status)
    {
        var seeded = await SeedAsync(EventPrivacy.PUBLIC, status);
        using var scope = factory.Services.CreateScope();
        var events = scope.ServiceProvider.GetRequiredService<EventsService>();

        Assert.Equal(status == EventStatus.PUBLISHED, (await events.GetAsync(seeded.Event.Id, seeded.ParticipantId)).Succeeded);
        Assert.Equal(status == EventStatus.PUBLISHED, (await events.GetAsync(seeded.Event.Id, seeded.StrangerId)).Succeeded);
        Assert.True((await events.GetAsync(seeded.Event.Id, seeded.OwnerId)).Succeeded);
    }

    [Fact]
    public async Task Deleted_cancelled_events_remain_hidden_from_everyone()
    {
        var seeded = await SeedAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var item = await db.Events.SingleAsync(item => item.Id == seeded.Event.Id);
        item.Delete(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        var events = scope.ServiceProvider.GetRequiredService<EventsService>();

        Assert.False((await events.GetAsync(item.Id, seeded.ParticipantId)).Succeeded);
        Assert.False((await events.GetAsync(item.Id, seeded.OwnerId)).Succeeded);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_block_in_either_direction_hides_cancelled_user_events_from_existing_participants(bool hostBlocks)
    {
        var seeded = await SeedAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.BlockedUsers.Add(new BlockedUser(
            hostBlocks ? seeded.OwnerId : seeded.ParticipantId,
            hostBlocks ? seeded.ParticipantId : seeded.OwnerId, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        Assert.False((await scope.ServiceProvider.GetRequiredService<EventsService>()
            .GetAsync(seeded.Event.Id, seeded.ParticipantId)).Succeeded);
    }

    [Fact]
    public async Task Cancelled_private_group_events_require_current_membership_as_well_as_existing_participation()
    {
        var seeded = await SeedAsync(privateGroup: true);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.GroupMembers.AddRange(
            new GroupMember(seeded.Event.HostId, seeded.ParticipantId, GroupMemberRole.MEMBER, DateTimeOffset.UtcNow),
            new GroupMember(seeded.Event.HostId, seeded.StrangerId, GroupMemberRole.MEMBER, DateTimeOffset.UtcNow));
        db.EventParticipants.Add(new EventParticipant(seeded.Event.Id, seeded.DeclinedId,
            EventParticipantStatus.INTERESTED, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        var events = scope.ServiceProvider.GetRequiredService<EventsService>();

        Assert.True((await events.GetAsync(seeded.Event.Id, seeded.ParticipantId)).Succeeded);
        Assert.False((await events.GetAsync(seeded.Event.Id, seeded.InviteeId)).Succeeded);
        Assert.False((await events.GetAsync(seeded.Event.Id, seeded.DeclinedId)).Succeeded);
        Assert.False((await events.GetAsync(seeded.Event.Id, seeded.StrangerId)).Succeeded);

        db.GroupMembers.Remove(await db.GroupMembers.SingleAsync(item =>
            item.GroupId == seeded.Event.HostId && item.UserId == seeded.ParticipantId));
        await db.SaveChangesAsync();
        Assert.False((await events.GetAsync(seeded.Event.Id, seeded.ParticipantId)).Succeeded);
    }

    [Fact]
    public async Task Cancelled_page_events_follow_the_hosts_published_member_and_deleted_visibility()
    {
        var seeded = await SeedAsync(pageHost: true);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var events = scope.ServiceProvider.GetRequiredService<EventsService>();
        var page = await db.Pages.SingleAsync(item => item.Id == seeded.Event.HostId);

        Assert.True((await events.GetAsync(seeded.Event.Id, seeded.ParticipantId)).Succeeded);
        page.Unpublish(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        Assert.False((await events.GetAsync(seeded.Event.Id, seeded.ParticipantId)).Succeeded);

        db.PageMembers.Add(new PageMember(page.Id, seeded.ParticipantId, PageRole.MODERATOR, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        Assert.True((await events.GetAsync(seeded.Event.Id, seeded.ParticipantId)).Succeeded);
        Assert.False((await events.GetAsync(seeded.Event.Id, seeded.InviteeId)).Succeeded);

        page.Delete(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        Assert.False((await events.GetAsync(seeded.Event.Id, seeded.ParticipantId)).Succeeded);
    }

    private async Task<SeededEvent> SeedAsync(
        EventPrivacy privacy = EventPrivacy.PUBLIC,
        EventStatus status = EventStatus.CANCELLED,
        bool privateGroup = false,
        bool pageHost = false)
    {
        var now = DateTimeOffset.UtcNow;
        var users = Enumerable.Range(0, 5).Select(index => new User(Guid.NewGuid(),
            $"cancel-access-{Guid.NewGuid():N}@example.com", $"cancel_access_{Guid.NewGuid():N}"[..32], now.AddTicks(index))).ToArray();
        var hostId = privateGroup || pageHost ? Guid.NewGuid() : users[0].Id;
        var item = new Event(Guid.NewGuid(), "Cancelled event access", null,
            privateGroup ? EventHostType.GROUP : pageHost ? EventHostType.PAGE : EventHostType.USER, hostId, users[0].Id, privacy,
            EventLocationType.PHYSICAL, "Hanoi", null, null, now.AddDays(7), null, status, now);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.Users.AddRange(users);
        db.UserProfiles.AddRange(users.Select(user => new UserProfile(user.Id, user.UserName!, now)));
        if (privateGroup)
        {
            db.Groups.Add(new Group(hostId, "Private event group", null, GroupPrivacy.PRIVATE, users[0].Id, now));
            db.GroupMembers.Add(new GroupMember(hostId, users[0].Id, GroupMemberRole.OWNER, now));
        }
        if (pageHost)
        {
            var page = new Page(hostId, "Cancelled event page", "cancel_event_" + Guid.NewGuid().ToString("N")[..12],
                "Community", null, users[0].Id, now);
            page.Publish(now);
            db.Pages.Add(page);
            db.PageMembers.Add(new PageMember(hostId, users[0].Id, PageRole.OWNER, now));
        }
        db.Events.Add(item);
        db.EventParticipants.Add(new EventParticipant(item.Id, users[1].Id, EventParticipantStatus.GOING, now));
        db.EventInvitations.Add(new EventInvitation(Guid.NewGuid(), item.Id, users[0].Id, users[2].Id, now));
        var declined = new EventInvitation(Guid.NewGuid(), item.Id, users[0].Id, users[3].Id, now);
        declined.Decline(now);
        db.EventInvitations.Add(declined);
        await db.SaveChangesAsync();
        return new(item, users[0].Id, users[1].Id, users[2].Id, users[3].Id, users[4].Id);
    }

    private sealed record SeededEvent(Event Event, Guid OwnerId, Guid ParticipantId, Guid InviteeId, Guid DeclinedId, Guid StrangerId);
}
