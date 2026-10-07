using Fookbase.Api.Modules.Events.Domain.Enums;
using Fookbase.Api.Modules.Groups.Domain.Enums;
using Fookbase.Api.Modules.Media.Domain.Enums;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Fookbase.Api.Modules.Events.DTOs.Responses;
using Fookbase.Api.Modules.Events.Entities;
using Fookbase.Api.Modules.Groups.Entities;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Notifications.DTOs.Responses;
using Fookbase.Api.Modules.Pages.Domain.Enums;
using Fookbase.Api.Modules.Pages.Entities;
using Fookbase.Api.Modules.Posts.DTOs.Responses;
using Fookbase.Api.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class EventEndpointsTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    [Theory]
    [InlineData("USER")]
    [InlineData("0")]
    public async Task Valid_annotations_preserve_normalized_text_online_urls_offsets_and_default_status(string hostType)
    {
        var userId = (await CreateUsersAsync(1))[0];
        using var client = CreateAuthenticatedClient(userId);
        var name = new string('a', 160);
        var description = new string('b', 10_000);
        var response = await client.PostAsJsonAsync("/api/events", new
        {
            hostType,
            name = "  " + name + "  ",
            description = "  " + description + "  ",
            privacy = "PUBLIC",
            locationType = "ONLINE",
            onlineUrl = "  https://example.com/join  ",
            startsAtUtc = "2027-01-01T07:00:00+07:00",
            endsAtUtc = "2027-01-01T08:00:00+07:00"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var item = await ReadAsync<EventResponse>(response);
        Assert.Equal(name, item.Name);
        Assert.Equal(description, item.Description);
        Assert.Equal("https://example.com/join", item.OnlineUrl);
        Assert.Equal(DateTimeOffset.Parse("2027-01-01T00:00:00+00:00"), item.StartsAtUtc);
        Assert.Equal(DateTimeOffset.Parse("2027-01-01T01:00:00+00:00"), item.EndsAtUtc);
        Assert.Equal("published", item.Status);
        Assert.Equal(userId, item.DisplayHost.Id);
    }

    [Fact]
    public async Task Event_cover_relationships_support_attaching_replacing_and_removing_ready_images()
    {
        var userId = (await CreateUsersAsync(1))[0];
        var mediaIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            var now = DateTimeOffset.UtcNow;
            foreach (var mediaId in mediaIds)
            {
                var asset = new MediaAsset(mediaId, userId, MediaType.IMAGE, $"{userId:N}/{mediaId:N}.png",
                    "cover.png", "image/png", 11, now, now.AddMinutes(5));
                asset.MarkReady(11, now);
                db.MediaAssets.Add(asset);
            }
            await db.SaveChangesAsync();
        }
        using var client = CreateAuthenticatedClient(userId);
        var item = await ReadAsync<EventResponse>(await client.PostAsJsonAsync("/api/events", new
        {
            hostType = "user", name = "Event cover", privacy = "public", locationType = "physical",
            startsAtUtc = DateTimeOffset.UtcNow.AddDays(7), coverMediaId = mediaIds[0]
        }));
        Assert.Equal($"/api/events/{item.Id}/cover", item.CoverUrl);
        var replaced = await ReadAsync<EventResponse>(await client.PatchAsJsonAsync($"/api/events/{item.Id}", new
        {
            item.Name, item.Privacy, item.LocationType, item.StartsAtUtc, coverMediaId = mediaIds[1]
        }));
        Assert.Equal(item.CoverUrl, replaced.CoverUrl);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            Assert.Equal(mediaIds[1], (await db.Events.AsNoTracking().SingleAsync(row => row.Id == item.Id)).CoverMediaId);
            Assert.Equal(mediaIds[1], (await db.EventCoverMediaReferences.AsNoTracking()
                .SingleAsync(row => row.EventId == item.Id)).MediaId);
        }
        var removed = await ReadAsync<EventResponse>(await client.PatchAsJsonAsync($"/api/events/{item.Id}", new
        {
            item.Name, item.Privacy, item.LocationType, item.StartsAtUtc, removeCover = true
        }));
        Assert.Null(removed.CoverUrl);
        using var verification = factory.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.False(await context.EventCoverMediaReferences.AnyAsync(row => row.EventId == item.Id));
        Assert.Equal(2, await context.MediaAssets.CountAsync(row => mediaIds.Contains(row.Id)));
    }

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
    public async Task User_event_hosts_and_participants_include_the_uploaded_avatar_path()
    {
        var users = await CreateUsersAsync(2);
        var ownerId = users[0];
        var attendeeId = users[1];
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            var now = DateTimeOffset.UtcNow;
            foreach (var userId in users)
            {
                var mediaId = Guid.NewGuid();
                var asset = new MediaAsset(
                    mediaId, userId, MediaType.IMAGE, $"{userId:N}/{mediaId:N}.png",
                    "avatar.png", "image/png", 11, now, now.AddMinutes(5));
                asset.MarkReady(11, now);
                db.MediaAssets.Add(asset);
                var profile = await db.UserProfiles.SingleAsync(item => item.UserId == userId);
                profile.Update(null, null, null, null, mediaId, null, now);
            }
            await db.SaveChangesAsync();
        }

        using var owner = CreateAuthenticatedClient(ownerId);
        using var attendee = CreateAuthenticatedClient(attendeeId);
        var item = await CreateEventAsync(owner, "public");
        await attendee.PostAsJsonAsync($"/api/events/{item.Id}/rsvp", new { status = "going" });
        var participants = await ReadAsync<EventCursorPageResponse<EventParticipantResponse>>(
            await owner.GetAsync($"/api/events/{item.Id}/participants?limit=20"));

        Assert.Equal($"/api/users/{ownerId}/avatar", item.DisplayHost.AvatarUrl);
        Assert.Equal($"/api/users/{attendeeId}/avatar", Assert.Single(participants.Items).AvatarUrl);
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
            x.Status == EventParticipantStatus.GOING));
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
            db.Groups.Add(new Group(groupId, "event group", null, GroupPrivacy.PUBLIC, users[0], DateTimeOffset.UtcNow));
            db.GroupMembers.AddRange(new GroupMember(groupId, users[0], GroupMemberRole.ADMIN, DateTimeOffset.UtcNow),
                new GroupMember(groupId, users[1], GroupMemberRole.MEMBER, DateTimeOffset.UtcNow));
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
            db.Pages.Add(new Page(pageId, "Event Page", "event_page_" + Guid.NewGuid().ToString("N")[..8], "Community", null,
                manager, DateTimeOffset.UtcNow));
            db.PageMembers.Add(new PageMember(pageId, manager, PageRole.EDITOR, DateTimeOffset.UtcNow));
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

    [Theory]
    [InlineData("mine")]
    [InlineData("upcoming")]
    [InlineData("discover")]
    public async Task Event_lists_advance_with_equal_start_times(string endpoint)
    {
        var owner = (await CreateUsersAsync(1))[0];
        var prefix = $"pagination-{Guid.NewGuid():N}";
        var events = await SeedEventsAsync(owner, prefix);
        using var client = CreateAuthenticatedClient(owner);
        var path = $"/api/events/{endpoint}" + (endpoint == "discover" ? $"?query={prefix}" : "");

        var items = await ReadPagesAsync<EventResponse>(client, path, events.Length, 2);

        Assert.Equal(events.Select(item => item.Id).Order(), items.Select(item => item.Id));
        Assert.All(items, item => Assert.Equal(owner, item.DisplayHost.Id));
    }

    [Fact]
    public async Task Invitation_pages_load_multiple_event_responses_and_advance()
    {
        var users = await CreateUsersAsync(2);
        var events = await SeedEventsAsync(users[0], $"invitations-{Guid.NewGuid():N}");
        var now = DateTimeOffset.UtcNow;
        var invitations = events.Select(item =>
            new EventInvitation(Guid.NewGuid(), item.Id, users[0], users[1], now)).ToArray();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            db.EventInvitations.AddRange(invitations);
            await db.SaveChangesAsync();
        }
        using var client = CreateAuthenticatedClient(users[1]);

        var items = await ReadPagesAsync<EventInvitationResponse>(client, "/api/events/invitations/mine", 3, 2);

        Assert.Equal(invitations.Select(item => item.Id).OrderDescending(), items.Select(item => item.Id));
        Assert.All(items, item => Assert.NotNull(item.Event));
    }

    [Theory]
    [InlineData("participants")]
    [InlineData("posts")]
    public async Task Event_content_pages_advance_with_equal_timestamps(string endpoint)
    {
        var users = await CreateUsersAsync(4);
        var item = (await SeedEventsAsync(users[0], $"content-{Guid.NewGuid():N}"))[0];
        var now = DateTimeOffset.UtcNow;
        using var client = CreateAuthenticatedClient(users[0]);
        if (endpoint == "participants")
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            db.EventParticipants.AddRange(users.Skip(1).Select(user =>
                new EventParticipant(item.Id, user, EventParticipantStatus.GOING, now)));
            await db.SaveChangesAsync();
            var participants = await ReadPagesAsync<EventParticipantResponse>(client, $"/api/events/{item.Id}/participants", 3, 1);
            Assert.Equal(users.Skip(1).OrderDescending(), participants.Select(participant => participant.UserId));
        }
        else
        {
            var ids = new List<Guid>();
            for (var index = 0; index < 3; index++)
            {
                var post = await ReadAsync<PostResponse>(await client.PostAsJsonAsync(
                    $"/api/events/{item.Id}/posts", new { content = $"Pagination post {index}" }));
                ids.Add(post.Id);
            }
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
                await db.Posts.Where(post => ids.Contains(post.Id)).ExecuteUpdateAsync(
                    setters => setters.SetProperty(post => post.CreatedAtUtc, now));
            }
            var posts = await ReadPagesAsync<PostResponse>(client, $"/api/events/{item.Id}/posts", 3, 1);
            Assert.Equal(ids.OrderDescending(), posts.Select(post => post.Id));
        }
    }

    [Fact]
    public async Task Event_list_endpoints_reject_invalid_cursors()
    {
        var owner = (await CreateUsersAsync(1))[0];
        var item = (await SeedEventsAsync(owner, $"invalid-{Guid.NewGuid():N}"))[0];
        using var client = CreateAuthenticatedClient(owner);
        foreach (var path in new[] { "mine", "upcoming", "discover", "invitations/mine", $"{item.Id}/participants", $"{item.Id}/posts" })
        {
            using var response = await client.GetAsync($"/api/events/{path}?cursor=invalid-cursor");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    private async Task<Event[]> SeedEventsAsync(Guid owner, string prefix)
    {
        var now = DateTimeOffset.UtcNow;
        var events = Enumerable.Range(0, 3).Select(index => new Event(
            Guid.NewGuid(), $"{prefix}-{index}", null, EventHostType.USER, owner, owner,
            EventPrivacy.PUBLIC, EventLocationType.PHYSICAL, "Hanoi", null, null,
            now.AddDays(7), null, EventStatus.PUBLISHED, now)).ToArray();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.Events.AddRange(events);
        await db.SaveChangesAsync();
        return events;
    }

    private static async Task<List<T>> ReadPagesAsync<T>(HttpClient client, string path, int count, int limit)
    {
        var items = new List<T>();
        string? cursor = null;
        for (var index = 0; index < count; index++)
        {
            var url = path + (path.Contains('?') ? "&" : "?") + $"limit={limit}" +
                (cursor is null ? "" : $"&cursor={Uri.EscapeDataString(cursor)}");
            var page = await ReadAsync<EventCursorPageResponse<T>>(await client.GetAsync(url));
            items.AddRange(page.Items);
            cursor = page.NextCursor;
            if (cursor is null) break;
        }
        Assert.Null(cursor);
        Assert.Equal(count, items.Count);
        return items;
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
        db.UserProfiles.AddRange(users.Select(user => new UserProfile(user.Id, user.UserName!, DateTimeOffset.UtcNow)));
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
