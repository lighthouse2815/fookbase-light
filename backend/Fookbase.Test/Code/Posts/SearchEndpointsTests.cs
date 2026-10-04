using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Fookbase.Api.Modules.Friends.Entities;
using Fookbase.Api.Modules.Groups.Entities;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Pages.Entities;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Search.DTOs.Responses;
using Fookbase.Api.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class SearchEndpointsTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    [Fact]
    public async Task Search_requires_authentication_and_rejects_invalid_input()
    {
        using var anonymous = factory.CreateClient();
        var userId = await CreateUserAsync("search-validation");
        using var user = CreateAuthenticatedClient(userId);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/search?q=valid")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.GetAsync("/api/search?q=%20%20")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.GetAsync("/api/search?q=a")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.GetAsync("/api/search?q=valid&type=unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.GetAsync("/api/search?q=valid&type=people&cursor=not-a-cursor")).StatusCode);
    }

    [Fact]
    public async Task People_search_is_case_insensitive_excludes_two_way_blocks_and_never_exposes_identity_fields()
    {
        var viewerId = await CreateUserAsync("search-viewer");
        var targetId = await CreateUserAsync("ALICE.search");
        await UpdateProfileAsync(targetId, "Alice Search", "This short bio is safe to display.");
        using var viewer = CreateAuthenticatedClient(viewerId);

        var beforeBlock = await ReadAsync<GlobalSearchResponse>(
            await viewer.GetAsync("/api/search?q=ALICE&type=people"));
        var raw = await (await viewer.GetAsync("/api/search?q=alice&type=people")).Content.ReadAsStringAsync();
        await BlockAsync(targetId, viewerId);
        var afterBlock = await ReadAsync<GlobalSearchResponse>(
            await viewer.GetAsync("/api/search?q=alice&type=people"));

        Assert.Contains(beforeBlock.People, item => item.UserId == targetId && item.DisplayName == "Alice Search");
        Assert.DoesNotContain("Email", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("security", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(afterBlock.People, item => item.UserId == targetId);
    }

    [Fact]
    public async Task People_search_does_not_search_or_return_email_and_phone_identifiers()
    {
        var viewerId = await CreateUserAsync("private-contact-viewer");
        var emailUserId = Guid.NewGuid();
        var phoneUserId = Guid.NewGuid();
        const string email = "private.user@example.com";
        const string phone = "0912345678";
        var now = DateTimeOffset.UtcNow;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            var emailProfile = new UserProfile(emailUserId, email, now);
            var phoneProfile = new UserProfile(phoneUserId, phone, now);
            emailProfile.Update("Private email", null, null, null, null, null, now);
            phoneProfile.Update("Private phone", null, null, null, null, null, now);
            db.Users.AddRange(
                new User(emailUserId, email, email, now),
                new User(phoneUserId, "phone-owner@example.com", phone, now));
            db.UserProfiles.AddRange(emailProfile, phoneProfile);
            await db.SaveChangesAsync();
        }

        using var viewer = CreateAuthenticatedClient(viewerId);
        var response = await viewer.GetAsync("/api/search?q=private&type=people");
        var raw = await response.Content.ReadAsStringAsync();
        var result = await ReadAsync<GlobalSearchResponse>(response);
        var byEmail = await ReadAsync<GlobalSearchResponse>(
            await viewer.GetAsync($"/api/search?q={Uri.EscapeDataString(email)}&type=people"));
        var byPhone = await ReadAsync<GlobalSearchResponse>(
            await viewer.GetAsync($"/api/search?q={phone}&type=people"));

        Assert.DoesNotContain(email, raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(phone, raw, StringComparison.Ordinal);
        Assert.All(result.People.Where(item => item.UserId == emailUserId || item.UserId == phoneUserId), item =>
            Assert.Equal(string.Empty, item.Username));
        Assert.Empty(byEmail.People);
        Assert.Empty(byPhone.People);
    }

    [Fact]
    public async Task People_search_projects_follow_and_friendship_state_without_changing_order()
    {
        var viewerId = await CreateUserAsync("people-projection-viewer");
        var firstTargetId = await CreateUserAsync("people-projection-first");
        var secondTargetId = await CreateUserAsync("people-projection-second");
        var viewerBlockedRelationId = await CreateUserAsync("people-projection-viewer-blocked");
        var query = "projection" + Guid.NewGuid().ToString("N")[..10];
        var now = DateTimeOffset.UtcNow;
        await UpdateProfileAsync(firstTargetId, query + " Alpha", "first");
        await UpdateProfileAsync(secondTargetId, query + " Beta", "second");
        using var viewer = CreateAuthenticatedClient(viewerId);

        var before = await ReadAsync<GlobalSearchResponse>(
            await viewer.GetAsync($"/api/search?q={query}&type=people"));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            db.UserFollows.AddRange(
                UserFollow.Create(viewerId, secondTargetId, now),
                UserFollow.Create(viewerBlockedRelationId, secondTargetId, now),
                UserFollow.Create(secondTargetId, viewerBlockedRelationId, now));
            await db.SaveChangesAsync();
        }
        await BlockAsync(viewerId, viewerBlockedRelationId);

        var after = await ReadAsync<GlobalSearchResponse>(
            await viewer.GetAsync($"/api/search?q={query}&type=people"));

        Assert.Equal(before.People.Select(item => item.UserId), after.People.Select(item => item.UserId));
        var first = Assert.Single(after.People, item => item.UserId == firstTargetId);
        Assert.Equal(0, first.FollowerCount);
        Assert.Equal(0, first.FollowingCount);
        Assert.False(first.IsFollowing);
        Assert.False(first.IsFollowedBy);
        Assert.Equal("none", first.FriendshipState);
        var second = Assert.Single(after.People, item => item.UserId == secondTargetId);
        Assert.Equal(1, second.FollowerCount);
        Assert.Equal(0, second.FollowingCount);
        Assert.True(second.IsFollowing);
        Assert.False(second.IsFollowedBy);
        Assert.Equal("none", second.FriendshipState);
    }

    [Fact]
    public async Task Group_and_page_search_follow_existing_visibility_without_manager_leaks()
    {
        var viewerId = await CreateUserAsync("search-container-viewer");
        var ownerId = await CreateUserAsync("search-container-owner");
        var now = DateTimeOffset.UtcNow;
        var publicGroup = Group.Create(Guid.NewGuid(), "Needle public group", "search description", GroupPrivacy.PUBLIC, ownerId, now);
        var privateGroup = Group.Create(Guid.NewGuid(), "Needle private group", "member only", GroupPrivacy.PRIVATE, ownerId, now);
        var publishedPage = Page.Create(Guid.NewGuid(), "Needle public Page", "needle.public.page", "Community", "search bio", ownerId, now);
        publishedPage.Publish(now);
        var unpublishedPage = Page.Create(Guid.NewGuid(), "Needle hidden Page", "needle.hidden.page", "Community", null, ownerId, now);
        var pagePost = Post.CreateInContainer(Guid.NewGuid(), ownerId, "needle page post", PostPrivacy.PUBLIC,
            PostContainerType.PAGE, publishedPage.Id, now, PostType.STANDARD);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            db.Groups.AddRange(publicGroup, privateGroup);
            db.GroupMembers.AddRange(
                GroupMember.Create(publicGroup.Id, ownerId, GroupMemberRole.OWNER, now),
                GroupMember.Create(privateGroup.Id, ownerId, GroupMemberRole.OWNER, now));
            db.Pages.AddRange(publishedPage, unpublishedPage);
            db.PageMembers.AddRange(
                PageMember.Create(publishedPage.Id, ownerId, PageRole.OWNER, now),
                PageMember.Create(unpublishedPage.Id, ownerId, PageRole.OWNER, now));
            db.Posts.Add(pagePost);
            await db.SaveChangesAsync();
        }
        using var viewer = CreateAuthenticatedClient(viewerId);

        var groupsBeforeMembership = await ReadAsync<GlobalSearchResponse>(
            await viewer.GetAsync("/api/search?q=needle&type=groups"));
        var pages = await ReadAsync<GlobalSearchResponse>(
            await viewer.GetAsync("/api/search?q=needle&type=pages"));
        var postsAfterOwnerBlock = await ReadAsync<GlobalSearchResponse>(
            await viewer.GetAsync("/api/search?q=needle&type=posts"));

        Assert.Contains(groupsBeforeMembership.Groups, item => item.GroupId == publicGroup.Id);
        Assert.DoesNotContain(groupsBeforeMembership.Groups, item => item.GroupId == privateGroup.Id);
        Assert.Contains(pages.Pages, item => item.PageId == publishedPage.Id);
        Assert.DoesNotContain(pages.Pages, item => item.PageId == unpublishedPage.Id);
        Assert.Contains(postsAfterOwnerBlock.Posts, item => item.PostId == pagePost.Id && item.AuthorUserId is null);
        Assert.DoesNotContain("CreatedByUserId", await (await viewer.GetAsync("/api/search?q=needle&type=pages")).Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            db.GroupMembers.Add(GroupMember.Create(privateGroup.Id, viewerId, GroupMemberRole.MEMBER, now));
            db.BlockedUsers.Add(BlockedUser.Create(viewerId, ownerId, now));
            await db.SaveChangesAsync();
        }
        var groupsAfterMembership = await ReadAsync<GlobalSearchResponse>(
            await viewer.GetAsync("/api/search?q=needle&type=groups"));
        var pagePostsAfterOwnerBlock = await ReadAsync<GlobalSearchResponse>(
            await viewer.GetAsync("/api/search?q=needle&type=posts"));

        Assert.Contains(groupsAfterMembership.Groups, item => item.GroupId == privateGroup.Id);
        Assert.Contains(pagePostsAfterOwnerBlock.Posts, item => item.PostId == pagePost.Id);
    }

    [Fact]
    public async Task Post_and_reel_search_enforce_privacy_block_media_and_cursor_rules()
    {
        var viewerId = await CreateUserAsync("search-content-viewer");
        var authorId = await CreateUserAsync("search-content-author");
        var now = DateTimeOffset.UtcNow;
        var publicPost = Post.Create(Guid.NewGuid(), authorId, "Needle public post", PostPrivacy.PUBLIC, now);
        var secondPublicPost = Post.Create(Guid.NewGuid(), authorId, "Needle second public post", PostPrivacy.PUBLIC, now.AddTicks(1));
        var friendsPost = Post.Create(Guid.NewGuid(), authorId, "Needle friends post", PostPrivacy.FRIENDS, now.AddTicks(2));
        var onlyMePost = Post.Create(Guid.NewGuid(), authorId, "Needle only me post", PostPrivacy.ONLY_ME, now.AddTicks(3));
        var reel = Post.CreateReel(Guid.NewGuid(), authorId, "Needle reel caption", PostPrivacy.PUBLIC, now.AddTicks(4));
        var mediaId = Guid.NewGuid();
        var media = MediaAsset.CreatePending(mediaId, authorId, MediaType.VIDEO, "reel.mp4", "reel.mp4", "video/mp4", 20, now, now.AddMinutes(5));
        media.MarkProcessing(20, now);
        media.MarkVideoReady("processed.mp4", "poster.jpg", 1_000, 720, 1_280, now);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            db.Posts.AddRange(publicPost, secondPublicPost, friendsPost, onlyMePost, reel);
            db.MediaAssets.Add(media);
            db.PostMedia.Add(PostMedia.Create(reel.Id, mediaId, 0));
            await db.SaveChangesAsync();
        }
        using var viewer = CreateAuthenticatedClient(viewerId);

        var posts = await ReadAsync<GlobalSearchResponse>(
            await viewer.GetAsync("/api/search?q=needle&type=posts&limit=1"));
        var reels = await ReadAsync<GlobalSearchResponse>(
            await viewer.GetAsync("/api/search?q=needle&type=reels"));

        Assert.Contains(posts.Posts, item => item.PostId == publicPost.Id || item.PostId == secondPublicPost.Id);
        Assert.DoesNotContain(posts.Posts, item => item.PostId == friendsPost.Id || item.PostId == onlyMePost.Id);
        Assert.NotNull(posts.NextCursor);
        Assert.Equal(HttpStatusCode.BadRequest, (await viewer.GetAsync(
            "/api/search?q=other&type=posts&cursor=" + Uri.EscapeDataString(posts.NextCursor!))).StatusCode);
        Assert.Contains(reels.Reels, item => item.ReelId == reel.Id && item.Media.PosterAccessPath.EndsWith("/poster/access"));

        await BlockAsync(viewerId, authorId);
        var blockedPosts = await ReadAsync<GlobalSearchResponse>(
            await viewer.GetAsync("/api/search?q=needle&type=posts"));
        var blockedReels = await ReadAsync<GlobalSearchResponse>(
            await viewer.GetAsync("/api/search?q=needle&type=reels"));

        Assert.DoesNotContain(blockedPosts.Posts, item => item.PostId == publicPost.Id);
        Assert.DoesNotContain(blockedReels.Reels, item => item.ReelId == reel.Id);
    }

    [Fact]
    public async Task All_search_is_bounded_and_suggestions_only_include_lightweight_types()
    {
        var viewerId = await CreateUserAsync("search-preview-viewer");
        var now = DateTimeOffset.UtcNow;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            for (var index = 0; index < 6; index++)
            {
                var ownerId = await CreateUserAsync("search-preview-owner-" + index);
                var group = Group.Create(Guid.NewGuid(), "Preview group " + index, null, GroupPrivacy.PUBLIC, ownerId, now.AddTicks(index));
                db.Groups.Add(group);
                db.GroupMembers.Add(GroupMember.Create(group.Id, ownerId, GroupMemberRole.OWNER, now));
            }
            await db.SaveChangesAsync();
        }
        using var viewer = CreateAuthenticatedClient(viewerId);

        var all = await ReadAsync<GlobalSearchResponse>(await viewer.GetAsync("/api/search?q=preview&type=all&limit=50"));
        var suggestions = await ReadAsync<SearchSuggestionsResponse>(await viewer.GetAsync("/api/search/suggestions?q=preview"));

        Assert.True(all.Groups.Count <= 5);
        Assert.Null(all.NextCursor);
        Assert.True(suggestions.Groups.Count <= 5);
    }

    private async Task<Guid> CreateUserAsync(string username)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.Users.Add(new User(id, username + "-" + id.ToString("N") + "@example.com", username[..Math.Min(username.Length, 32)], now));
        db.UserProfiles.Add(new UserProfile(id, username[..Math.Min(username.Length, 32)], now));
        await db.SaveChangesAsync();
        return id;
    }

    private async Task UpdateProfileAsync(Guid userId, string displayName, string bio)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var profile = await db.UserProfiles.SingleAsync(item => item.UserId == userId);
        profile.Update(displayName, bio, null, null, null, null, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
    }

    private async Task BlockAsync(Guid blockerId, Guid blockedId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.BlockedUsers.Add(BlockedUser.Create(blockerId, blockedId, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
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
