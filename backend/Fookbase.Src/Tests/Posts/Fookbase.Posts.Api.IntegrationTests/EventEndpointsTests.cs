using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Fookbase.Api.Modules.Events.DTOs.Responses;
using Fookbase.Api.Modules.Events.Entities;
using Fookbase.Api.Modules.Groups.Entities;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Notifications.DTOs.Responses;
using Fookbase.Api.Modules.Pages.Entities;
using Fookbase.Api.Modules.Posts.DTOs.Responses;
using Fookbase.Api.Modules.Users.Entities;
using Fookbase.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class EventEndpointsTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    [Fact]
    public async Task Private_event_and_its_discussion_post_are_hidden_from_an_unrelated_user()
    {
        var users = await CreateUsersAsync(2);
        using var owner = CreateAuthenticatedClient(users[0]);
        using var stranger = CreateAuthenticatedClient(users[1]);
        var item = await CreateEventAsync(owner, "private");
        var post = await ReadAsync<PostResponse>(await owner.PostAsJsonAsync(
            $"/api/events/{item.Id}/posts", new { content = "private discussion", mediaIds = Array.Empty<Guid>() }));

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/events/{item.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/posts/{post.Id}")).StatusCode);
    }

    [Fact]
    public async Task Published_event_rsvp_changes_state_and_aggregate_counts()
    {
        var users = await CreateUsersAsync(2);
        using var owner = CreateAuthenticatedClient(users[0]);
        using var attendee = CreateAuthenticatedClient(users[1]);
        var item = await CreateEventAsync(owner, "public");

        var going = await ReadAsync<EventResponse>(await attendee.PostAsJsonAsync(
            $"/api/events/{item.Id}/rsvp", new { status = "going" }));
        var interested = await ReadAsync<EventResponse>(await attendee.PostAsJsonAsync(
            $"/api/events/{item.Id}/rsvp", new { status = "interested" }));

        Assert.Equal("going", going.ViewerRsvpStatus);
        Assert.Equal(1, going.GoingCount);
        Assert.Equal("interested", interested.ViewerRsvpStatus);
        Assert.Equal(0, interested.GoingCount);
        Assert.Equal(1, interested.InterestedCount);
    }

    [Fact]
    public async Task Accepting_an_event_invitation_creates_a_going_participant()
    {
        var users = await CreateUsersAsync(2);
        using var owner = CreateAuthenticatedClient(users[0]);
        using var invitee = CreateAuthenticatedClient(users[1]);
        var item = await CreateEventAsync(owner, "private");
        var invitation = await ReadAsync<EventInvitationResponse>(await owner.PostAsJsonAsync(
            $"/api/events/{item.Id}/invites", new { userId = users[1] }));

        var accepted = await ReadAsync<EventInvitationResponse>(await invitee.PostAsync(
            $"/api/events/invitations/{invitation.Id}/accept", null));

        Assert.Equal("accepted", accepted.Status);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.True(await db.EventParticipants.AnyAsync(x => x.EventId == item.Id && x.UserId == users[1] &&
            x.Status == EventParticipantStatus.Going));
    }

    [Fact]
    public async Task Event_invite_notification_includes_its_event_id()
    {
        var users = await CreateUsersAsync(2);
        using var owner = CreateAuthenticatedClient(users[0]);
        using var invitee = CreateAuthenticatedClient(users[1]);
        var item = await CreateEventAsync(owner, "public");
        var invitation = await ReadAsync<EventInvitationResponse>(await owner.PostAsJsonAsync(
            $"/api/events/{item.Id}/invites", new { userId = users[1] }));

        var notifications = await ReadAsync<NotificationPageResponse>(
            await invitee.GetAsync("/api/notifications"));

        var notification = Assert.Single(notifications.Items, x => x.Type == "EventInvite");
        Assert.Equal(invitation.Id, notification.EntityId);
        Assert.Equal(item.Id, notification.ParentEntityId);
    }

    [Fact]
    public async Task Group_admin_can_create_an_event_but_regular_member_cannot()
    {
        var users = await CreateUsersAsync(2);
        var groupId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            db.Groups.Add(Group.Create(groupId, "event group", null, GroupPrivacy.Public, users[0], DateTimeOffset.UtcNow));
            db.GroupMembers.AddRange(GroupMember.Create(groupId, users[0], GroupMemberRole.Admin, DateTimeOffset.UtcNow),
                GroupMember.Create(groupId, users[1], GroupMemberRole.Member, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        using var admin = CreateAuthenticatedClient(users[0]);
        using var member = CreateAuthenticatedClient(users[1]);

        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/events", EventBody("group", groupId))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync("/api/events", EventBody("group", groupId))).StatusCode);
    }

    [Fact]
    public async Task Page_hosted_event_exposes_page_identity_without_creator_identity()
    {
        var manager = (await CreateUsersAsync(1))[0];
        var pageId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            db.Pages.Add(Page.Create(pageId, "Event Page", "event_page_" + Guid.NewGuid().ToString("N")[..8], "Community", null,
                manager, DateTimeOffset.UtcNow));
            db.PageMembers.Add(PageMember.Create(pageId, manager, PageRole.Editor, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        using var client = CreateAuthenticatedClient(manager);
        var response = await client.PostAsJsonAsync("/api/events", EventBody("page", pageId));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var host = document.RootElement.GetProperty("displayHost");
        Assert.Equal("page", host.GetProperty("type").GetString());
        Assert.Equal(pageId, host.GetProperty("id").GetGuid());
        Assert.False(host.TryGetProperty("createdByUserId", out _));
        Assert.False(document.RootElement.TryGetProperty("createdByUserId", out _));
    }

    private static object EventBody(string hostType, Guid? hostId = null) => new
    {
        hostType,
        hostId,
        name = "Integration event " + Guid.NewGuid().ToString("N"),
        description = "Event test",
        privacy = "public",
        locationType = "physical",
        locationName = "Hanoi",
        startsAtUtc = DateTimeOffset.UtcNow.AddDays(7),
        status = "published"
    };

    private async Task<EventResponse> CreateEventAsync(HttpClient client, string privacy) =>
        await ReadAsync<EventResponse>(await client.PostAsJsonAsync("/api/events", new
        {
            hostType = "user", name = "Event " + Guid.NewGuid().ToString("N"), privacy,
            locationType = "physical", locationName = "Hanoi", startsAtUtc = DateTimeOffset.UtcNow.AddDays(7), status = "published"
        }));

    private async Task<Guid[]> CreateUsersAsync(int count)
    {
        var users = Enumerable.Range(0, count).Select(index => new User(Guid.NewGuid(),
            $"events-{Guid.NewGuid():N}@example.com", $"events_{Guid.NewGuid():N}"[..32], DateTimeOffset.UtcNow.AddTicks(index))).ToArray();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.Users.AddRange(users);
        db.UserProfiles.AddRange(users.Select(user => UserProfile.Create(user.Id, user.UserName!, DateTimeOffset.UtcNow)));
        await db.SaveChangesAsync();
        return users.Select(user => user.Id).ToArray();
    }

    private HttpClient CreateAuthenticatedClient(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"],
            [new(JwtRegisteredClaimNames.Sub, userId.ToString()), new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())],
            now.AddSeconds(-1), now.AddMinutes(5), new SigningCredentials(new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    { response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<T>() ?? throw new InvalidOperationException("Response body was empty."); }
}
