using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Fookbase.Api.Modules.Feed.DTOs.Responses;
using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Notifications.DTOs.Responses;
using Fookbase.Api.Modules.Notifications.Entities;
using Fookbase.Api.Modules.Pages.DTOs.Responses;
using Fookbase.Api.Modules.Pages.Domain.Enums;
using Fookbase.Api.Modules.Pages.Entities;
using Fookbase.Api.Modules.Posts.DTOs.Responses;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class PageEndpointsTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    [Theory]
    [InlineData("ab")]
    [InlineData("name with spaces")]
    [InlineData("name-with-dash")]
    [InlineData("name/with/slash")]
    [InlineData("name@domain")]
    [InlineData("UPPER CASE SPACE")]
    [InlineData("this.username.is.longer.than.the.allowed.fifty.character.maximum")]
    public async Task Invalid_page_usernames_are_rejected(string username)
    {
        var ownerId = (await CreateUsersAsync(1))[0];
        using var owner = CreateAuthenticatedClient(ownerId);

        var response = await owner.PostAsJsonAsync("/api/pages", new { name = "Invalid username", username, category = "Test" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("api")]
    [InlineData("feed")]
    [InlineData("groups")]
    [InlineData("media")]
    [InlineData("messages")]
    [InlineData("pages")]
    [InlineData("posts")]
    [InlineData("stories")]
    public async Task Reserved_page_usernames_are_rejected(string username)
    {
        var ownerId = (await CreateUsersAsync(1))[0];
        using var owner = CreateAuthenticatedClient(ownerId);

        var response = await owner.PostAsJsonAsync("/api/pages", new { name = "Reserved username", username, category = "Test" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/pages")]
    [InlineData("PATCH", "/api/pages/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/pages/00000000-0000-0000-0000-000000000001/follow")]
    [InlineData("DELETE", "/api/pages/00000000-0000-0000-0000-000000000001/follow")]
    [InlineData("POST", "/api/pages/invitations/00000000-0000-0000-0000-000000000001/accept")]
    public async Task Page_management_commands_require_authentication(string method, string path)
    {
        using var client = factory.CreateClient();
        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_validates_username_and_creates_an_unpublished_owner_page()
    {
        var ownerId = (await CreateUsersAsync(1))[0];
        using var owner = CreateAuthenticatedClient(ownerId);

        var invalid = await owner.PostAsJsonAsync("/api/pages", new { name = "Invalid", username = "No spaces", category = "Test" });
        var created = await CreatePageAsync(owner, "builders.page");

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("unpublished", created.Status);
        Assert.Equal("owner", created.ViewerRole);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Contains(await db.PageMembers.Where(member => member.PageId == created.Id).ToListAsync(),
            member => member.UserId == ownerId && member.Role == PageRole.OWNER);
    }

    [Fact]
    public async Task Username_is_normalized_reserved_and_database_case_insensitive()
    {
        var users = await CreateUsersAsync(2);
        using var first = CreateAuthenticatedClient(users[0]);
        using var second = CreateAuthenticatedClient(users[1]);

        var firstPage = await first.PostAsJsonAsync("/api/pages", new { name = "First", username = "Case.Page", category = "Test" });
        var duplicate = await second.PostAsJsonAsync("/api/pages", new { name = "Second", username = "case.page", category = "Test" });
        var reserved = await second.PostAsJsonAsync("/api/pages", new { name = "Reserved", username = "api", category = "Test" });

        Assert.Equal(HttpStatusCode.Created, firstPage.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, reserved.StatusCode);
    }

    [Fact]
    public async Task Published_page_is_public_and_unpublished_page_is_manager_only()
    {
        var users = await CreateUsersAsync(2);
        using var owner = CreateAuthenticatedClient(users[0]);
        using var outsider = CreateAuthenticatedClient(users[1]);
        var page = await CreatePageAsync(owner, "visibility.page");

        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/pages/{page.Username}")).StatusCode);
        (await owner.PostAsync($"/api/pages/{page.Id}/publish", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await outsider.GetAsync($"/api/pages/{page.Username}")).StatusCode);
        (await owner.PostAsync($"/api/pages/{page.Id}/unpublish", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/pages/{page.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/pages/{page.Id}")).StatusCode);
    }

    [Fact]
    public async Task Follow_and_unfollow_are_idempotent_and_only_published_pages_accept_followers()
    {
        var users = await CreateUsersAsync(2);
        using var owner = CreateAuthenticatedClient(users[0]);
        using var follower = CreateAuthenticatedClient(users[1]);
        var page = await CreatePageAsync(owner, "follow.page");

        Assert.Equal(HttpStatusCode.NotFound, (await follower.PostAsync($"/api/pages/{page.Id}/follow", null)).StatusCode);
        (await owner.PostAsync($"/api/pages/{page.Id}/publish", null)).EnsureSuccessStatusCode();
        (await follower.PostAsync($"/api/pages/{page.Id}/follow", null)).EnsureSuccessStatusCode();
        (await follower.PostAsync($"/api/pages/{page.Id}/follow", null)).EnsureSuccessStatusCode();
        (await follower.DeleteAsync($"/api/pages/{page.Id}/follow")).EnsureSuccessStatusCode();
        (await follower.DeleteAsync($"/api/pages/{page.Id}/follow")).EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Empty(await db.PageFollowers.Where(item => item.PageId == page.Id && item.UserId == users[1]).ToListAsync());
    }

    [Fact]
    public async Task Invitations_notify_invitee_and_acceptance_creates_the_requested_role()
    {
        var users = await CreateUsersAsync(3);
        using var owner = CreateAuthenticatedClient(users[0]);
        using var invitee = CreateAuthenticatedClient(users[1]);
        var page = await CreatePageAsync(owner, "invite.page");

        var created = await owner.PostAsJsonAsync($"/api/pages/{page.Id}/invitations", new { userId = users[1], role = "editor" });
        var invitation = await ReadAsync<PageRoleInvitationResponse>(created);
        var duplicate = await owner.PostAsJsonAsync($"/api/pages/{page.Id}/invitations", new { userId = users[1], role = "editor" });
        var mine = await ReadAsync<PageCursorPageResponse<PageRoleInvitationResponse>>(await invitee.GetAsync("/api/pages/invitations/mine"));
        var notifications = await ReadAsync<NotificationPageResponse>(await invitee.GetAsync("/api/notifications"));
        var inviteNotification = Assert.Single(notifications.Items, item => item.Type == "PageRoleInvite");
        Assert.Equal(invitation.Id, inviteNotification.EntityId);
        Assert.Equal(page.Id, inviteNotification.ParentEntityId);
        Assert.Equal(page.Username, inviteNotification.PageUsername);
        await RecordingNotificationHubContext.AssertPublishedResponseAsync(factory.Services, inviteNotification);
        var accepted = await invitee.PostAsync($"/api/pages/invitations/{invitation.Id}/accept", null);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains(mine.Items, item => item.Id == invitation.Id);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Contains(await db.PageMembers.Where(item => item.PageId == page.Id).ToListAsync(),
            item => item.UserId == users[1] && item.Role == PageRole.EDITOR);
        Assert.Contains(await db.Notifications.ToListAsync(), notification => notification.RecipientUserId == users[1] &&
            notification.Type == NotificationType.PAGE_ROLE_INVITE && notification.EntityId == invitation.Id);
    }

    [Fact]
    public async Task Admins_cannot_manage_owners_but_owner_can_transfer_ownership_atomically()
    {
        var users = await CreateUsersAsync(3);
        using var owner = CreateAuthenticatedClient(users[0]);
        using var admin = CreateAuthenticatedClient(users[1]);
        var page = await CreatePageAsync(owner, "roles.page");
        await AddManagerAsync(owner, page.Id, users[1], "admin");
        await AddManagerAsync(owner, page.Id, users[2], "editor");

        var adminRemovesOwner = await admin.DeleteAsync($"/api/pages/{page.Id}/members/{users[0]}");
        var ownerTransfers = await owner.PostAsJsonAsync($"/api/pages/{page.Id}/transfer-ownership", new { userId = users[2] });

        Assert.Equal(HttpStatusCode.Forbidden, adminRemovesOwner.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, ownerTransfers.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Contains(await db.PageMembers.Where(item => item.PageId == page.Id).ToListAsync(), item => item.UserId == users[2] && item.Role == PageRole.OWNER);
        Assert.Contains(await db.PageMembers.Where(item => item.PageId == page.Id).ToListAsync(), item => item.UserId == users[0] && item.Role == PageRole.ADMIN);
    }

    [Fact]
    public async Task Page_posts_display_page_identity_without_revealing_the_publishing_manager()
    {
        var users = await CreateUsersAsync(3);
        using var owner = CreateAuthenticatedClient(users[0]);
        using var viewer = CreateAuthenticatedClient(users[1]);
        var page = await CreatePageAsync(owner, "identity.page");
        (await owner.PostAsync($"/api/pages/{page.Id}/publish", null)).EnsureSuccessStatusCode();
        var created = await owner.PostAsJsonAsync($"/api/pages/{page.Id}/posts", new { content = "Posted as Page", privacy = "onlyMe", mediaIds = Array.Empty<Guid>() });
        var post = await ReadAsync<PostResponse>(created);
        using (var scope = factory.Services.CreateScope())
        {
            var friends = scope.ServiceProvider.GetRequiredService<FriendsService>();
            Assert.True((await friends.BlockAsync(users[1], users[0])).Succeeded);

            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            var persisted = await db.Posts.SingleAsync(item => item.Id == post.Id);
            Assert.Equal(users[0], persisted.AuthorUserId);
            Assert.Equal(PostContainerType.PAGE, persisted.ContainerType);
            Assert.Equal(page.Id, persisted.ContainerId);
        }

        var direct = await ReadAsync<PostResponse>(await viewer.GetAsync($"/api/posts/{post.Id}"));

        Assert.Null(post.AuthorUserId);
        Assert.Equal("page", post.DisplayAuthor!.Type);
        Assert.Equal(page.Username, post.DisplayAuthor.Username);
        Assert.Null(direct.AuthorUserId);
        Assert.Equal("page", direct.ContainerType);
    }

    [Fact]
    public async Task Owners_admins_and_editors_can_publish_page_posts_with_user_audit_authors()
    {
        var users = await CreateUsersAsync(5);
        using var owner = CreateAuthenticatedClient(users[0]);
        using var admin = CreateAuthenticatedClient(users[1]);
        using var editor = CreateAuthenticatedClient(users[2]);
        using var moderator = CreateAuthenticatedClient(users[3]);
        using var follower = CreateAuthenticatedClient(users[4]);
        var page = await CreatePageAsync(owner, "publisher.roles.page");
        (await owner.PostAsync($"/api/pages/{page.Id}/publish", null)).EnsureSuccessStatusCode();
        await AddManagerAsync(owner, page.Id, users[1], "admin");
        await AddManagerAsync(owner, page.Id, users[2], "editor");
        await AddManagerAsync(owner, page.Id, users[3], "moderator");
        (await follower.PostAsync($"/api/pages/{page.Id}/follow", null)).EnsureSuccessStatusCode();

        var ownerPost = await ReadAsync<PostResponse>(await owner.PostAsJsonAsync(
            $"/api/pages/{page.Id}/posts", new { content = "owner", mediaIds = Array.Empty<Guid>() }));
        var adminPost = await ReadAsync<PostResponse>(await admin.PostAsJsonAsync(
            $"/api/pages/{page.Id}/posts", new { content = "admin", mediaIds = Array.Empty<Guid>() }));
        var editorPost = await ReadAsync<PostResponse>(await editor.PostAsJsonAsync(
            $"/api/pages/{page.Id}/posts", new { content = "editor", mediaIds = Array.Empty<Guid>() }));
        var moderatorPost = await moderator.PostAsJsonAsync(
            $"/api/pages/{page.Id}/posts", new { content = "moderator", mediaIds = Array.Empty<Guid>() });
        var followerPost = await follower.PostAsJsonAsync(
            $"/api/pages/{page.Id}/posts", new { content = "follower", mediaIds = Array.Empty<Guid>() });

        Assert.Equal(HttpStatusCode.Forbidden, moderatorPost.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, followerPost.StatusCode);
        Assert.All(new[] { ownerPost, adminPost, editorPost }, post =>
        {
            Assert.Null(post.AuthorUserId);
            Assert.Equal("page", post.DisplayAuthor!.Type);
            Assert.Equal(page.Id, post.DisplayAuthor.Id);
        });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var persisted = await db.Posts.Where(item => item.Id == ownerPost.Id || item.Id == adminPost.Id || item.Id == editorPost.Id)
            .ToDictionaryAsync(item => item.Id);
        Assert.Equal(users[0], persisted[ownerPost.Id].AuthorUserId);
        Assert.Equal(users[1], persisted[adminPost.Id].AuthorUserId);
        Assert.Equal(users[2], persisted[editorPost.Id].AuthorUserId);
        Assert.All(persisted.Values, item =>
        {
            Assert.Equal(PostContainerType.PAGE, item.ContainerType);
            Assert.Equal(page.Id, item.ContainerId);
        });
    }

    [Fact]
    public async Task Page_post_engagement_notifies_the_publishing_manager_but_not_the_manager_self()
    {
        var users = await CreateUsersAsync(2);
        using var owner = CreateAuthenticatedClient(users[0]);
        using var visitor = CreateAuthenticatedClient(users[1]);
        var page = await CreatePageAsync(owner, "page.notifications.page");
        (await owner.PostAsync($"/api/pages/{page.Id}/publish", null)).EnsureSuccessStatusCode();
        var post = await ReadAsync<PostResponse>(await owner.PostAsJsonAsync(
            $"/api/pages/{page.Id}/posts", new { content = "notify", mediaIds = Array.Empty<Guid>() }));

        (await visitor.PutAsJsonAsync($"/api/posts/{post.Id}/reaction", new { type = "like" })).EnsureSuccessStatusCode();
        (await visitor.PostAsJsonAsync($"/api/posts/{post.Id}/comments", new { content = "visitor comment" })).EnsureSuccessStatusCode();
        (await owner.PutAsJsonAsync($"/api/posts/{post.Id}/reaction", new { type = "love" })).EnsureSuccessStatusCode();
        (await owner.PostAsJsonAsync($"/api/posts/{post.Id}/comments", new { content = "owner comment" })).EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var notifications = await db.Notifications.AsNoTracking().Where(item => item.EntityId == post.Id).ToListAsync();
        Assert.Contains(notifications, item => item.RecipientUserId == users[0] && item.ActorUserId == users[1] &&
            item.Type == NotificationType.POST_REACTION);
        Assert.Contains(notifications, item => item.RecipientUserId == users[0] && item.ActorUserId == users[1] &&
            item.Type == NotificationType.POST_COMMENT);
        Assert.DoesNotContain(notifications, item => item.ActorUserId == users[0] &&
            (item.Type == NotificationType.POST_REACTION || item.Type == NotificationType.POST_COMMENT));
    }

    [Fact]
    public async Task Page_posts_are_not_added_to_the_home_feed_and_timeline_uses_keyset_order()
    {
        var ownerId = (await CreateUsersAsync(1))[0];
        using var owner = CreateAuthenticatedClient(ownerId);
        var page = await CreatePageAsync(owner, "timeline.page");
        (await owner.PostAsync($"/api/pages/{page.Id}/publish", null)).EnsureSuccessStatusCode();
        var first = await ReadAsync<PostResponse>(await owner.PostAsJsonAsync($"/api/pages/{page.Id}/posts", new { content = "first", privacy = "public", mediaIds = Array.Empty<Guid>() }));
        var second = await ReadAsync<PostResponse>(await owner.PostAsJsonAsync($"/api/pages/{page.Id}/posts", new { content = "second", privacy = "public", mediaIds = Array.Empty<Guid>() }));
        var timeline = await ReadAsync<PageTimelineResponse>(await owner.GetAsync($"/api/pages/{page.Id}/posts?limit=1"));
        var secondTimeline = await ReadAsync<PageTimelineResponse>(await owner.GetAsync($"/api/pages/{page.Id}/posts?limit=1&cursor={Uri.EscapeDataString(timeline.NextCursor!)}"));
        var home = await ReadAsync<FeedPageResponse>(await owner.GetAsync("/api/feed"));

        Assert.Equal(second.Id, timeline.Items.Single().Id);
        Assert.Equal(first.Id, secondTimeline.Items.Single().Id);
        Assert.DoesNotContain(home.Items, item => item.Id == first.Id || item.Id == second.Id);
    }

    [Fact]
    public async Task Page_moderators_can_remove_comments_but_cannot_publish_posts()
    {
        var users = await CreateUsersAsync(3);
        using var owner = CreateAuthenticatedClient(users[0]);
        using var moderator = CreateAuthenticatedClient(users[1]);
        using var commenter = CreateAuthenticatedClient(users[2]);
        var page = await CreatePageAsync(owner, "moderation.page");
        (await owner.PostAsync($"/api/pages/{page.Id}/publish", null)).EnsureSuccessStatusCode();
        await AddManagerAsync(owner, page.Id, users[1], "moderator");
        var post = await ReadAsync<PostResponse>(await owner.PostAsJsonAsync($"/api/pages/{page.Id}/posts", new { content = "moderate", privacy = "public", mediaIds = Array.Empty<Guid>() }));
        var comment = await commenter.PostAsJsonAsync($"/api/posts/{post.Id}/comments", new { content = "comment" });
        var commentResponse = await ReadAsync<Fookbase.Api.Modules.Posts.DTOs.Responses.CommentResponse>(comment);

        var publish = await moderator.PostAsJsonAsync($"/api/pages/{page.Id}/posts", new { content = "not allowed", privacy = "public", mediaIds = Array.Empty<Guid>() });
        var deleted = await moderator.DeleteAsync($"/api/posts/comments/{commentResponse.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, publish.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task Page_avatar_and_cover_references_block_deletion_and_release_on_replacement()
    {
        var ownerId = (await CreateUsersAsync(1))[0];
        using var owner = CreateAuthenticatedClient(ownerId);
        var page = await CreatePageAsync(owner, "media.page");
        var avatar = await AddReadyImageAsync(ownerId);
        var cover = await AddReadyImageAsync(ownerId);
        var setMedia = await owner.PatchAsJsonAsync($"/api/pages/{page.Id}/media", new { avatarMediaId = avatar, coverMediaId = cover });
        setMedia.EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope();
        var media = scope.ServiceProvider.GetRequiredService<MediaService>();
        Assert.False((await media.DeleteAsync(ownerId, avatar)).Succeeded);
        var replacement = await AddReadyImageAsync(ownerId);
        (await owner.PatchAsJsonAsync($"/api/pages/{page.Id}/media", new { avatarMediaId = replacement })).EnsureSuccessStatusCode();
        Assert.True((await media.DeleteAsync(ownerId, avatar)).Succeeded);
    }

    [Theory]
    [InlineData("Name", "Tên trang là bắt buộc.")]
    [InlineData("Username", "Tên người dùng của trang là bắt buộc.")]
    [InlineData("Category", "Danh mục trang là bắt buộc.")]
    public async Task Page_requests_validate_required_fields_before_writing(string field, string message)
    {
        using var owner = CreateAuthenticatedClient((await CreateUsersAsync(1))[0]);
        foreach (var value in new string?[] { null, string.Empty, "   " })
        {
            var request = new Dictionary<string, object?>
            {
                ["Name"] = "A Page", ["Username"] = "annotation.page", ["Category"] = "Integration"
            };
            request[field] = value;
            foreach (var update in new[] { false, true })
            {
                using var response = update
                    ? await owner.PatchAsJsonAsync($"/api/pages/{Guid.NewGuid()}", request)
                    : await owner.PostAsJsonAsync("/api/pages", request);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.Contains(body.RootElement.GetProperty("errors").GetProperty(field).EnumerateArray(),
                    error => error.GetString() == message);
            }
        }
    }

    [Fact]
    public async Task Page_manager_requests_reject_empty_user_ids_and_blank_roles()
    {
        using var owner = CreateAuthenticatedClient((await CreateUsersAsync(1))[0]);
        var cases = new (string Suffix, string Field, object Value, string Message)[]
        {
            ("invitations", "UserId", Guid.Empty, "Người dùng là bắt buộc."),
            ("transfer-ownership", "UserId", Guid.Empty, "Người dùng là bắt buộc."),
            ("invitations", "Role", "   ", "Vai trò trên trang là bắt buộc."),
            ($"members/{Guid.NewGuid()}/role", "Role", "   ", "Vai trò trên trang là bắt buộc.")
        };
        foreach (var item in cases)
        {
            var request = new Dictionary<string, object?> { ["UserId"] = Guid.NewGuid(), ["Role"] = "editor" };
            request[item.Field] = item.Value;
            var path = $"/api/pages/{Guid.NewGuid()}/{item.Suffix}";
            using var response = item.Suffix.StartsWith("members/")
                ? await owner.PatchAsJsonAsync(path, request)
                : await owner.PostAsJsonAsync(path, request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Contains(body.RootElement.GetProperty("errors").GetProperty(item.Field).EnumerateArray(),
                error => error.GetString() == item.Message);
        }
    }

    [Fact]
    public async Task Page_name_length_validation_uses_trimmed_length()
    {
        using var owner = CreateAuthenticatedClient((await CreateUsersAsync(1))[0]);
        var username = "limits." + Guid.NewGuid().ToString("N");
        using var invalid = await owner.PostAsJsonAsync("/api/pages", new
        {
            name = new string('a', Page.MaximumNameLength + 1), username, category = "Integration"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using (var body = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync()))
        {
            Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("Name", out _));
        }
        var page = await ReadAsync<PageResponse>(await owner.PostAsJsonAsync("/api/pages", new
        {
            name = " " + new string('a', Page.MaximumNameLength) + " ", username, category = " Integration "
        }));
        Assert.Equal(new string('a', Page.MaximumNameLength), page.Name);
        Assert.Equal("Integration", page.Category);
    }

    [Fact]
    public async Task Page_relationships_load_and_cascade_only_page_dependents()
    {
        var users = await CreateUsersAsync(2);
        var avatar = await AddReadyImageAsync(users[0]);
        var cover = await AddReadyImageAsync(users[0]);
        var now = DateTimeOffset.UtcNow;
        var pageId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            var page = Page.Create(pageId, "Mapping Page", "mapping." + pageId.ToString("N"), "Integration", null, users[0], now);
            page.SetMedia(avatar, cover, now);
            db.Pages.Add(page);
            db.PageMembers.Add(PageMember.Create(pageId, users[0], PageRole.OWNER, now));
            db.PageFollowers.Add(PageFollower.Create(pageId, users[1], now));
            db.PageRoleInvitations.Add(PageRoleInvitation.Create(Guid.NewGuid(), pageId, users[0], users[1], PageRole.EDITOR, now));
            db.PageMediaReferences.AddRange(PageMediaReference.Create(pageId, PageMediaSlot.AVATAR, avatar, now),
                PageMediaReference.Create(pageId, PageMediaSlot.COVER, cover, now));
            await db.SaveChangesAsync();
        }
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            var page = await db.Pages.Include(item => item.CreatedByUser)
                .Include(item => item.AvatarMedia).Include(item => item.CoverMedia)
                .Include(item => item.Members).ThenInclude(item => item.User)
                .Include(item => item.Followers).ThenInclude(item => item.User)
                .Include(item => item.RoleInvitations).ThenInclude(item => item.InviterUser)
                .Include(item => item.RoleInvitations).ThenInclude(item => item.InviteeUser)
                .Include(item => item.MediaReferences).ThenInclude(item => item.Media)
                .AsSplitQuery().SingleAsync(item => item.Id == pageId);
            Assert.Equal(users[0], page.CreatedByUser.Id);
            Assert.Equal(avatar, page.AvatarMedia!.Id);
            Assert.Equal(cover, page.CoverMedia!.Id);
            Assert.Equal(users[0], Assert.Single(page.Members).User.Id);
            Assert.Equal(users[1], Assert.Single(page.Followers).User.Id);
            var invitation = Assert.Single(page.RoleInvitations);
            Assert.Equal(users[0], invitation.InviterUser.Id);
            Assert.Equal(users[1], invitation.InviteeUser.Id);
            Assert.Equal(2, page.MediaReferences.Count);
            Assert.All(page.MediaReferences, item => { Assert.Same(page, item.Page); Assert.Equal(item.MediaId, item.Media.Id); });
            var userDelete = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
                db.Users.Where(item => item.Id == users[0]).ExecuteDeleteAsync());
            var mediaDelete = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
                db.MediaAssets.Where(item => item.Id == avatar).ExecuteDeleteAsync());
            Assert.Equal(Npgsql.PostgresErrorCodes.ForeignKeyViolation, userDelete.SqlState);
            Assert.Equal(Npgsql.PostgresErrorCodes.ForeignKeyViolation, mediaDelete.SqlState);
        }
        // Delete without loading dependents to exercise database cascades.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            await db.Pages.Where(item => item.Id == pageId).ExecuteDeleteAsync();
        }
        using var afterDelete = factory.Services.CreateScope();
        var afterDeleteDb = afterDelete.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.False(await afterDeleteDb.PageMembers.AnyAsync(item => item.PageId == pageId));
        Assert.False(await afterDeleteDb.PageFollowers.AnyAsync(item => item.PageId == pageId));
        Assert.False(await afterDeleteDb.PageRoleInvitations.AnyAsync(item => item.PageId == pageId));
        Assert.False(await afterDeleteDb.PageMediaReferences.AnyAsync(item => item.PageId == pageId));
        Assert.Equal(2, await afterDeleteDb.Users.CountAsync(item => users.Contains(item.Id)));
        Assert.Equal(2, await afterDeleteDb.MediaAssets.CountAsync(item => item.Id == avatar || item.Id == cover));
    }

    private async Task<PageResponse> CreatePageAsync(HttpClient client, string username) =>
        await ReadAsync<PageResponse>(await client.PostAsJsonAsync("/api/pages", new
        {
            name = "Page " + username,
            username,
            category = "Integration"
        }));

    private async Task AddManagerAsync(HttpClient owner, Guid pageId, Guid userId, string role)
    {
        var invitation = await ReadAsync<PageRoleInvitationResponse>(await owner.PostAsJsonAsync(
            $"/api/pages/{pageId}/invitations", new { userId, role }));
        using var invitee = CreateAuthenticatedClient(userId);
        (await invitee.PostAsync($"/api/pages/invitations/{invitation.Id}/accept", null)).EnsureSuccessStatusCode();
    }

    private async Task<Guid[]> CreateUsersAsync(int count)
    {
        var now = DateTimeOffset.UtcNow;
        var users = Enumerable.Range(0, count).Select(index => new User(Guid.NewGuid(),
            "pages-" + Guid.NewGuid().ToString("N") + "@example.com", ("pages_" + Guid.NewGuid().ToString("N"))[..32],
            now.AddTicks(index))).ToArray();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.Users.AddRange(users);
        db.UserProfiles.AddRange(users.Select(user => new UserProfile(user.Id, user.UserName!, now)));
        await db.SaveChangesAsync();
        return users.Select(user => user.Id).ToArray();
    }

    private async Task<Guid> AddReadyImageAsync(Guid ownerUserId)
    {
        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        var asset = MediaAsset.CreatePending(id, ownerUserId, MediaType.IMAGE, $"{ownerUserId:N}/{id:N}.png", "page.png", "image/png", 11, now, now.AddMinutes(5));
        asset.MarkReady(11, now);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync();
        return id;
    }

    private HttpClient CreateAuthenticatedClient(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"],
            [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()), new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())],
            notBefore: now.AddSeconds(-1), expires: now.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>() ?? throw new InvalidOperationException("Response body was empty.");
    }
}
