using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Fookbase.Api.Modules.Feed.DTOs.Responses;
using Fookbase.Api.Modules.Groups.DTOs.Responses;
using Fookbase.Api.Modules.Groups.Entities;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Notifications.Entities;
using Fookbase.Api.Modules.Posts.DTOs.Responses;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Persistence;
using Fookbase.Api.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class GroupEndpointsTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    [Fact]
    public async Task Creating_a_group_validates_input_and_creates_its_owner_membership_atomically()
    {
        var owner = (await CreateUsersAsync(1))[0];
        using var client = CreateAuthenticatedClient(owner);

        var invalid = await client.PostAsJsonAsync("/api/groups", new
        {
            name = "",
            description = "invalid",
            privacy = "public"
        });
        var created = await client.PostAsJsonAsync("/api/groups", new
        {
            name = "Fookbase builders",
            description = "A public group",
            privacy = "public"
        });
        var response = await ReadAsync<GroupResponse>(created);

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(owner, response.OwnerUserId);
        Assert.Equal(1, response.MemberCount);
        Assert.Equal("owner", response.ViewerRole);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var membership = await db.GroupMembers.SingleAsync(member => member.GroupId == response.Id);
        Assert.Equal(owner, membership.UserId);
        Assert.Equal(GroupMemberRole.Owner, membership.Role);
    }

    [Fact]
    public async Task Public_groups_join_immediately_and_owner_cannot_leave()
    {
        var users = await CreateUsersAsync(2);
        var group = await CreateGroupAsync(users[0], "public");
        using var member = CreateAuthenticatedClient(users[1]);
        using var owner = CreateAuthenticatedClient(users[0]);

        var joined = await member.PostAsync($"/api/groups/{group.Id}/join", null);
        var duplicate = await member.PostAsync($"/api/groups/{group.Id}/join", null);
        var members = await ReadAsync<GroupCursorPageResponse<GroupMemberResponse>>(
            await owner.GetAsync($"/api/groups/{group.Id}/members"));
        var left = await member.PostAsync($"/api/groups/{group.Id}/leave", null);
        var ownerLeave = await owner.PostAsync($"/api/groups/{group.Id}/leave", null);

        Assert.Equal(HttpStatusCode.OK, joined.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.All(members.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.DisplayName)));
        Assert.Contains(members.Items, item => item.UserId == users[1] && item.Username is not null);
        Assert.Equal(HttpStatusCode.NoContent, left.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, ownerLeave.StatusCode);
    }

    [Fact]
    public async Task Private_groups_create_requests_and_only_moderators_can_approve_or_decline()
    {
        var users = await CreateUsersAsync(3);
        var group = await CreateGroupAsync(users[0], "private");
        using var requester = CreateAuthenticatedClient(users[1]);
        using var outsider = CreateAuthenticatedClient(users[2]);
        using var owner = CreateAuthenticatedClient(users[0]);

        var requested = await requester.PostAsync($"/api/groups/{group.Id}/join", null);
        var request = await ReadAsync<GroupJoinRequestResponse>(requested);
        var duplicate = await requester.PostAsync($"/api/groups/{group.Id}/join", null);
        var forbidden = await outsider.PostAsync(
            $"/api/groups/{group.Id}/join-requests/{request.Id}/approve", null);
        var approved = await owner.PostAsync(
            $"/api/groups/{group.Id}/join-requests/{request.Id}/approve", null);
        var secondRequest = await outsider.PostAsync($"/api/groups/{group.Id}/join", null);
        var second = await ReadAsync<GroupJoinRequestResponse>(secondRequest);
        var declined = await owner.PostAsync(
            $"/api/groups/{group.Id}/join-requests/{second.Id}/decline", null);

        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal(HttpStatusCode.OK, declined.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Contains(await db.GroupMembers.Where(member => member.GroupId == group.Id).ToListAsync(),
            member => member.UserId == users[1] && member.Role == GroupMemberRole.Member);
        Assert.Contains(await db.Notifications.ToListAsync(), notification =>
            notification.RecipientUserId == users[1] &&
            notification.Type == NotificationType.GroupJoinApproved &&
            notification.EntityId == request.Id);
    }

    [Fact]
    public async Task Invites_prevent_duplicates_and_the_invitee_can_accept_or_decline()
    {
        var users = await CreateUsersAsync(4);
        var group = await CreateGroupAsync(users[0], "private");
        using var owner = CreateAuthenticatedClient(users[0]);
        using var firstInvitee = CreateAuthenticatedClient(users[1]);
        using var secondInvitee = CreateAuthenticatedClient(users[2]);

        var invited = await owner.PostAsJsonAsync(
            $"/api/groups/{group.Id}/invites",
            new { userId = users[1] });
        var invite = await ReadAsync<GroupInviteResponse>(invited);
        var pendingInvites = await ReadAsync<GroupCursorPageResponse<GroupInviteResponse>>(
            await firstInvitee.GetAsync("/api/groups/invites/mine"));
        var duplicate = await owner.PostAsJsonAsync(
            $"/api/groups/{group.Id}/invites",
            new { userId = users[1] });
        var accepted = await firstInvitee.PostAsync(
            $"/api/groups/{group.Id}/invites/{invite.Id}/accept", null);
        var secondInviteResponse = await owner.PostAsJsonAsync(
            $"/api/groups/{group.Id}/invites",
            new { userId = users[2] });
        var secondInvite = await ReadAsync<GroupInviteResponse>(secondInviteResponse);
        var declined = await secondInvitee.PostAsync(
            $"/api/groups/{group.Id}/invites/{secondInvite.Id}/decline", null);

        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        Assert.Contains(pendingInvites.Items, item => item.Id == invite.Id && item.GroupId == group.Id);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.OK, declined.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Contains(await db.GroupMembers.Where(member => member.GroupId == group.Id).ToListAsync(),
            member => member.UserId == users[1]);
        Assert.Contains(await db.Notifications.ToListAsync(), notification =>
            notification.RecipientUserId == users[1] &&
            notification.Type == NotificationType.GroupInvite &&
            notification.EntityId == invite.Id);
    }

    [Fact]
    public async Task Roles_prevent_privilege_escalation_and_protect_the_owner()
    {
        var users = await CreateUsersAsync(4);
        var group = await CreateGroupAsync(users[0], "public");
        await JoinAsync(group.Id, users[1]);
        await JoinAsync(group.Id, users[2]);
        using var owner = CreateAuthenticatedClient(users[0]);
        using var member = CreateAuthenticatedClient(users[1]);
        using var admin = CreateAuthenticatedClient(users[2]);

        var selfPromote = await member.PatchAsJsonAsync(
            $"/api/groups/{group.Id}/members/{users[1]}/role",
            new { role = "admin" });
        var promote = await owner.PatchAsJsonAsync(
            $"/api/groups/{group.Id}/members/{users[2]}/role",
            new { role = "admin" });
        var removeOwner = await admin.DeleteAsync($"/api/groups/{group.Id}/members/{users[0]}");
        var removeOrdinary = await admin.DeleteAsync($"/api/groups/{group.Id}/members/{users[1]}");
        var transfer = await owner.PatchAsJsonAsync(
            $"/api/groups/{group.Id}/members/{users[2]}/role",
            new { role = "owner" });
        var oldOwnerLeave = await owner.PostAsync($"/api/groups/{group.Id}/leave", null);

        Assert.Equal(HttpStatusCode.Forbidden, selfPromote.StatusCode);
        Assert.Equal(HttpStatusCode.OK, promote.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, removeOwner.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removeOrdinary.StatusCode);
        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, oldOwnerLeave.StatusCode);
    }

    [Fact]
    public async Task Private_group_content_and_home_feed_require_membership()
    {
        var users = await CreateUsersAsync(3);
        var group = await CreateGroupAsync(users[0], "private");
        await JoinAsync(group.Id, users[1], approve: true);
        using var owner = CreateAuthenticatedClient(users[0]);
        using var member = CreateAuthenticatedClient(users[1]);
        using var outsider = CreateAuthenticatedClient(users[2]);

        var created = await owner.PostAsJsonAsync($"/api/groups/{group.Id}/posts", new
        {
            content = "private group post",
            privacy = "friends",
            mediaIds = Array.Empty<Guid>()
        });
        var post = await ReadAsync<PostResponse>(created);
        var publicPost = await owner.PostAsJsonAsync("/api/posts", new
        {
            content = "profile post",
            privacy = "public",
            mediaIds = Array.Empty<Guid>()
        });

        var outsiderGroup = await outsider.GetAsync($"/api/groups/{group.Id}");
        var outsiderPosts = await outsider.GetAsync($"/api/groups/{group.Id}/posts");
        var outsiderDirect = await outsider.GetAsync($"/api/posts/{post.Id}");
        var memberDirect = await member.GetAsync($"/api/posts/{post.Id}");
        var nonMemberCreate = await outsider.PostAsJsonAsync($"/api/groups/{group.Id}/posts", new
        {
            content = "not permitted",
            privacy = "public",
            mediaIds = Array.Empty<Guid>()
        });
        var ownerFeed = await ReadAsync<FeedPageResponse>(await owner.GetAsync("/api/feed"));
        var memberFeed = await ReadAsync<FeedPageResponse>(await member.GetAsync("/api/feed"));
        var outsiderFeed = await ReadAsync<FeedPageResponse>(await outsider.GetAsync("/api/feed"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Created, publicPost.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderGroup.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderPosts.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderDirect.StatusCode);
        Assert.Equal(HttpStatusCode.OK, memberDirect.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, nonMemberCreate.StatusCode);
        Assert.Contains(ownerFeed.Items, item => item.Id == post.Id);
        Assert.Contains(memberFeed.Items, item => item.Id == post.Id);
        Assert.DoesNotContain(outsiderFeed.Items, item => item.Id == post.Id);
    }

    [Fact]
    public async Task Text_posts_in_groups_persist_selected_background()
    {
        var owner = (await CreateUsersAsync(1))[0];
        var group = await CreateGroupAsync(owner, "public");
        using var client = CreateAuthenticatedClient(owner);

        var createdResponse = await client.PostAsJsonAsync($"/api/groups/{group.Id}/posts", new
        {
            content = "Bài viết nhóm có nền",
            mediaIds = Array.Empty<Guid>(),
            textBackground = "sunset"
        });
        var created = await ReadAsync<PostResponse>(createdResponse);
        var posts = await ReadAsync<GroupCursorPageResponse<PostResponse>>(
            await client.GetAsync($"/api/groups/{group.Id}/posts"));

        Assert.Equal("sunset", created.TextBackground);
        Assert.Equal("sunset", Assert.Single(posts.Items, post => post.Id == created.Id).TextBackground);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Equal("sunset", (await db.Posts.SingleAsync(post => post.Id == created.Id)).TextBackground);
    }

    [Fact]
    public async Task Group_posts_keep_existing_comments_reactions_media_and_keyset_pagination()
    {
        var users = await CreateUsersAsync(2);
        var group = await CreateGroupAsync(users[0], "public");
        await JoinAsync(group.Id, users[1]);
        var mediaId = await AddReadyMediaAsync(users[0]);
        using var owner = CreateAuthenticatedClient(users[0]);
        using var member = CreateAuthenticatedClient(users[1]);

        var firstResponse = await owner.PostAsJsonAsync($"/api/groups/{group.Id}/posts", new
        {
            content = "",
            privacy = "public",
            mediaIds = new[] { mediaId }
        });
        var first = await ReadAsync<PostResponse>(firstResponse);
        var secondResponse = await owner.PostAsJsonAsync($"/api/groups/{group.Id}/posts", new
        {
            content = "second group post",
            privacy = "public",
            mediaIds = Array.Empty<Guid>()
        });
        var second = await ReadAsync<PostResponse>(secondResponse);
        var comment = await member.PostAsJsonAsync(
            $"/api/posts/{first.Id}/comments",
            new { content = "group comment" });
        var reaction = await member.PutAsJsonAsync(
            $"/api/posts/{first.Id}/reaction",
            new { type = "love" });
        var firstPage = await ReadAsync<GroupCursorPageResponse<PostResponse>>(
            await member.GetAsync($"/api/groups/{group.Id}/posts?limit=1"));
        var secondPage = await ReadAsync<GroupCursorPageResponse<PostResponse>>(
            await member.GetAsync(
                $"/api/groups/{group.Id}/posts?limit=1&cursor={Uri.EscapeDataString(firstPage.NextCursor!)}"));
        var mediaAccess = await member.GetAsync($"/api/posts/{first.Id}/media/{mediaId}/access");

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, comment.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reaction.StatusCode);
        Assert.Single(firstPage.Items);
        Assert.Single(secondPage.Items);
        Assert.NotEqual(firstPage.Items[0].Id, secondPage.Items[0].Id);
        Assert.Equal(HttpStatusCode.OK, mediaAccess.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var persisted = await db.Posts.SingleAsync(item => item.Id == first.Id);
        Assert.Equal(users[0], persisted.AuthorUserId);
        Assert.Equal(PostContainerType.Group, persisted.ContainerType);
        Assert.Equal(group.Id, persisted.ContainerId);
    }

    [Fact]
    public async Task Group_cover_references_prevent_deletion_and_deleted_groups_are_hidden()
    {
        var owner = (await CreateUsersAsync(1))[0];
        var group = await CreateGroupAsync(owner, "public");
        var mediaId = await AddReadyMediaAsync(owner);
        using var client = CreateAuthenticatedClient(owner);

        var updated = await client.PatchAsJsonAsync($"/api/groups/{group.Id}", new
        {
            name = group.Name,
            description = group.Description,
            privacy = "public",
            coverMediaId = mediaId
        });
        var deleteMedia = await client.DeleteAsync($"/api/media/{mediaId}");
        var deleted = await client.DeleteAsync($"/api/groups/{group.Id}");
        var missing = await client.GetAsync($"/api/groups/{group.Id}");

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, deleteMedia.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Administrator_updating_group_cover_creates_an_update_post_with_the_new_image()
    {
        var users = await CreateUsersAsync(2);
        var group = await CreateGroupAsync(users[0], "public");
        await JoinAsync(group.Id, users[1]);
        using var owner = CreateAuthenticatedClient(users[0]);
        Assert.Equal(HttpStatusCode.OK, (await owner.PatchAsJsonAsync(
            $"/api/groups/{group.Id}/members/{users[1]}/role", new { role = "admin" })).StatusCode);
        var mediaId = await AddReadyMediaAsync(users[1]);
        using var client = CreateAuthenticatedClient(users[1]);

        var updated = await client.PatchAsJsonAsync($"/api/groups/{group.Id}", new
        {
            name = group.Name,
            description = group.Description,
            privacy = group.Privacy,
            coverMediaId = mediaId
        });
        var posts = await ReadAsync<GroupCursorPageResponse<PostResponse>>(
            await client.GetAsync($"/api/groups/{group.Id}/posts"));

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var coverUpdate = Assert.Single(posts.Items);
        Assert.Equal(Post.CoverUpdatedPostContent, coverUpdate.Content);
        Assert.Equal(users[1], coverUpdate.AuthorUserId);
        Assert.Equal("group", coverUpdate.ContainerType);
        Assert.Equal(new[] { mediaId }, coverUpdate.MediaIds);
    }

    [Fact]
    public async Task Removing_group_cover_does_not_create_an_update_post_when_removal_overrides_a_media_id()
    {
        var owner = (await CreateUsersAsync(1))[0];
        var group = await CreateGroupAsync(owner, "public");
        var initialMediaId = await AddReadyMediaAsync(owner);
        var ignoredMediaId = await AddReadyMediaAsync(owner);
        using var client = CreateAuthenticatedClient(owner);

        var initialCover = await client.PatchAsJsonAsync($"/api/groups/{group.Id}", new
        {
            name = group.Name,
            description = group.Description,
            privacy = group.Privacy,
            coverMediaId = initialMediaId
        });
        var removed = await client.PatchAsJsonAsync($"/api/groups/{group.Id}", new
        {
            name = group.Name,
            description = group.Description,
            privacy = group.Privacy,
            coverMediaId = ignoredMediaId,
            removeCover = true
        });
        var updated = await ReadAsync<GroupResponse>(removed);
        var posts = await ReadAsync<GroupCursorPageResponse<PostResponse>>(
            await client.GetAsync($"/api/groups/{group.Id}/posts"));

        Assert.Equal(HttpStatusCode.OK, initialCover.StatusCode);
        Assert.Null(updated.CoverUrl);
        Assert.Equal(new[] { initialMediaId }, Assert.Single(posts.Items).MediaIds);
    }

    [Fact]
    public async Task Group_rules_follow_group_privacy_and_owner_or_admin_management()
    {
        var users = await CreateUsersAsync(3);
        var group = await CreateGroupAsync(users[0], "private");
        await JoinAsync(group.Id, users[1], approve: true);
        using var owner = CreateAuthenticatedClient(users[0]);
        using var member = CreateAuthenticatedClient(users[1]);
        using var outsider = CreateAuthenticatedClient(users[2]);

        var created = await owner.PostAsJsonAsync($"/api/groups/{group.Id}/rules", new
        {
            title = "Be kind",
            description = "Respect other members.",
            sortOrder = 2
        });
        var rule = await ReadAsync<GroupRuleResponse>(created);
        var memberRules = await member.GetAsync($"/api/groups/{group.Id}/rules");
        var outsiderRules = await outsider.GetAsync($"/api/groups/{group.Id}/rules");
        var unauthorizedUpdate = await member.PatchAsJsonAsync(
            $"/api/groups/{group.Id}/rules/{rule.Id}",
            new { title = "hijacked", description = "", sortOrder = 1 });
        var updated = await owner.PatchAsJsonAsync(
            $"/api/groups/{group.Id}/rules/{rule.Id}",
            new { title = "Be constructive", description = "Keep feedback useful.", sortOrder = 1 });
        var deleted = await owner.DeleteAsync($"/api/groups/{group.Id}/rules/{rule.Id}");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, memberRules.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderRules.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, unauthorizedUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task Blocking_a_group_post_author_hides_public_group_posts_from_the_blocker()
    {
        var users = await CreateUsersAsync(2);
        var group = await CreateGroupAsync(users[0], "public");
        await JoinAsync(group.Id, users[1]);
        using var owner = CreateAuthenticatedClient(users[0]);
        using var member = CreateAuthenticatedClient(users[1]);

        var created = await owner.PostAsJsonAsync($"/api/groups/{group.Id}/posts", new
        {
            content = "hidden after block",
            privacy = "public",
            mediaIds = Array.Empty<Guid>()
        });
        var post = await ReadAsync<PostResponse>(created);
        var blocked = await member.PostAsync($"/api/friends/blocks/{users[0]}", null);
        var feed = await ReadAsync<GroupCursorPageResponse<PostResponse>>(
            await member.GetAsync($"/api/groups/{group.Id}/posts"));
        var direct = await member.GetAsync($"/api/posts/{post.Id}");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, blocked.StatusCode);
        Assert.DoesNotContain(feed.Items, item => item.Id == post.Id);
        Assert.Equal(HttpStatusCode.NotFound, direct.StatusCode);
    }

    private async Task<GroupResponse> CreateGroupAsync(Guid ownerUserId, string privacy)
    {
        using var client = CreateAuthenticatedClient(ownerUserId);
        return await ReadAsync<GroupResponse>(await client.PostAsJsonAsync("/api/groups", new
        {
            name = "group-" + Guid.NewGuid().ToString("N"),
            description = "integration group",
            privacy
        }));
    }

    private async Task JoinAsync(Guid groupId, Guid userId, bool approve = false)
    {
        using var client = CreateAuthenticatedClient(userId);
        var response = await client.PostAsync($"/api/groups/{groupId}/join", null);
        if (!approve)
        {
            response.EnsureSuccessStatusCode();
            return;
        }

        var request = await ReadAsync<GroupJoinRequestResponse>(response);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var ownerId = await db.Groups.Where(group => group.Id == groupId)
            .Select(group => group.OwnerUserId)
            .SingleAsync();
        using var owner = CreateAuthenticatedClient(ownerId);
        (await owner.PostAsync($"/api/groups/{groupId}/join-requests/{request.Id}/approve", null))
            .EnsureSuccessStatusCode();
    }

    private async Task<Guid[]> CreateUsersAsync(int count)
    {
        var now = DateTimeOffset.UtcNow;
        var users = Enumerable.Range(0, count)
            .Select(index => new User(
                Guid.NewGuid(),
                "groups-" + Guid.NewGuid().ToString("N") + "@example.com",
                ("groups_" + Guid.NewGuid().ToString("N"))[..32],
                now.AddTicks(index)))
            .ToArray();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.Users.AddRange(users);
        db.UserProfiles.AddRange(users.Select(user =>
            UserProfile.Create(user.Id, user.UserName!, now)));
        await db.SaveChangesAsync();
        return users.Select(user => user.Id).ToArray();
    }

    private async Task<Guid> AddReadyMediaAsync(Guid ownerUserId)
    {
        var now = DateTimeOffset.UtcNow;
        var mediaId = Guid.NewGuid();
        var asset = MediaAsset.CreatePending(
            mediaId,
            ownerUserId,
            MediaType.Image,
            ownerUserId.ToString("N") + "/" + mediaId.ToString("N") + ".png",
            "group.png",
            "image/png",
            11,
            now,
            now.AddMinutes(5));
        asset.MarkReady(11, now);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync();
        return mediaId;
    }

    private HttpClient CreateAuthenticatedClient(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            configuration["Jwt:Issuer"],
            configuration["Jwt:Audience"],
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            notBefore: now.AddSeconds(-1),
            expires: now.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!)),
                SecurityAlgorithms.HmacSha256));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new InvalidOperationException("Response body was empty.");
    }
}
