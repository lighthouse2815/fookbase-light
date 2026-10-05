using Fookbase.Api.Modules.Media.Domain.Enums;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Fookbase.Api.Modules.Feed.DTOs.Responses;
using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Modules.Posts.DTOs.Responses;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class FeedEndpointsTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    [Theory]
    [InlineData("/api/feed?limit=20", true)]
    [InlineData("/api/feed/following?limit=20", true)]
    [InlineData("/api/feed?limit=20", false)]
    [InlineData("/api/feed/following?limit=20", false)]
    public async Task Feed_keeps_multiple_shares_of_the_same_post_without_duplicate_summaries(
        string endpoint, bool includeOriginal)
    {
        var users = await CreateUsersAsync(2);
        var viewer = users[0];
        var author = users[1];
        if (includeOriginal)
        {
            await CreateFriendshipAsync(viewer, author);
        }

        var original = await CreatePostAsync(author, PostPrivacy.PUBLIC, DateTimeOffset.UtcNow.AddMinutes(-1));
        using var client = CreateAuthenticatedClient(viewer);
        var shares = new List<PostShareResponse>();
        foreach (var caption in new[] { "Chia sẻ lần đầu", "Chia sẻ lần nữa" })
        {
            shares.Add(await ReadAsync<PostShareResponse>(await client.PostAsJsonAsync(
                $"/api/posts/{original.Id}/shares",
                new { destinationType = "profile", destinationId = viewer, caption })));
        }

        var feed = await ReadAsync<FeedPageResponse>(await client.GetAsync(endpoint));

        foreach (var share in shares)
        {
            var item = Assert.Single(feed.Items, item => item.Id == share.Id);
            Assert.Equal("share", item.ContentType);
            Assert.NotNull(item.Share);
            Assert.Equal(original.Id, item.Share.OriginalPostId);
            Assert.Equal(original.Id, item.Share.OriginalPost.Id);
            Assert.Equal(share.Caption, item.Share.Caption);
            Assert.Equal(2, item.Share.OriginalPost.ShareCount);
        }

        Assert.Equal(includeOriginal, feed.Items.Any(item => item.Id == original.Id));
        Assert.Equal(feed.Items.Count, feed.Items.Select(item => item.Id).Distinct().Count());
    }

    [Fact]
    public async Task Home_feed_respects_privacy_for_organic_and_suggested_posts()
    {
        var users = await CreateUsersAsync(3);
        var viewer = users[0];
        var friend = users[1];
        var stranger = users[2];
        await CreateFriendshipAsync(viewer, friend);
        var now = DateTimeOffset.UtcNow;
        var ownPublic = await CreatePostAsync(viewer, PostPrivacy.PUBLIC, now.AddMinutes(-1));
        var ownFriends = await CreatePostAsync(viewer, PostPrivacy.FRIENDS, now.AddMinutes(-2));
        var ownOnlyMe = await CreatePostAsync(viewer, PostPrivacy.ONLY_ME, now.AddMinutes(-3));
        var friendPublic = await CreatePostAsync(friend, PostPrivacy.PUBLIC, now.AddMinutes(-4));
        var friendFriends = await CreatePostAsync(friend, PostPrivacy.FRIENDS, now.AddMinutes(-5));
        var friendOnlyMe = await CreatePostAsync(friend, PostPrivacy.ONLY_ME, now.AddMinutes(-6));
        var strangerPublic = await CreatePostAsync(stranger, PostPrivacy.PUBLIC, now.AddMinutes(-7));
        var strangerFriends = await CreatePostAsync(stranger, PostPrivacy.FRIENDS, now.AddMinutes(-7));
        var strangerOnlyMe = await CreatePostAsync(stranger, PostPrivacy.ONLY_ME, now.AddMinutes(-7));
        var deleted = await CreatePostAsync(friend, PostPrivacy.PUBLIC, now.AddMinutes(-8));
        await DeletePostAsync(deleted.Id);
        using var client = CreateAuthenticatedClient(viewer);

        var feed = await ReadAsync<FeedPageResponse>(await client.GetAsync("/api/feed"));
        var ids = feed.Items.Select(item => item.Id).ToHashSet();

        Assert.Contains(ownPublic.Id, ids);
        Assert.Contains(ownFriends.Id, ids);
        Assert.Contains(ownOnlyMe.Id, ids);
        Assert.Contains(friendPublic.Id, ids);
        Assert.Contains(friendFriends.Id, ids);
        Assert.DoesNotContain(friendOnlyMe.Id, ids);
        Assert.DoesNotContain(strangerFriends.Id, ids);
        Assert.DoesNotContain(strangerOnlyMe.Id, ids);
        var suggestion = feed.Items.SingleOrDefault(item => item.Id == strangerPublic.Id);
        if (suggestion is not null)
        {
            Assert.True(suggestion.IsSuggested);
        }
        Assert.DoesNotContain(deleted.Id, ids);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(
            "/api/posts/" + friendOnlyMe.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(
            "/api/posts/" + friendFriends.Id)).StatusCode);
    }

    [Fact]
    public async Task Blocks_and_unfriend_remove_posts_from_future_home_feed_requests()
    {
        var users = await CreateUsersAsync(5);
        var viewer = users[0];
        var blockedFriend = users[1];
        var reverseBlockedFriend = users[2];
        var formerFriend = users[3];
        var controlFriend = users[4];
        await CreateFriendshipAsync(viewer, blockedFriend);
        await CreateFriendshipAsync(viewer, reverseBlockedFriend);
        await CreateFriendshipAsync(viewer, formerFriend);
        await CreateFriendshipAsync(viewer, controlFriend);
        var first = await CreatePostAsync(blockedFriend, PostPrivacy.FRIENDS, DateTimeOffset.UtcNow);
        var second = await CreatePostAsync(reverseBlockedFriend, PostPrivacy.PUBLIC, DateTimeOffset.UtcNow);
        var third = await CreatePostAsync(formerFriend, PostPrivacy.FRIENDS, DateTimeOffset.UtcNow);
        var control = await CreatePostAsync(controlFriend, PostPrivacy.FRIENDS, DateTimeOffset.UtcNow);
        await BlockAsync(viewer, blockedFriend);
        await BlockAsync(reverseBlockedFriend, viewer);
        await UnfriendAsync(viewer, formerFriend);
        using var client = CreateAuthenticatedClient(viewer);

        var feed = await ReadAsync<FeedPageResponse>(await client.GetAsync("/api/feed"));
        var ids = feed.Items.Select(item => item.Id).ToHashSet();

        Assert.DoesNotContain(first.Id, ids);
        Assert.DoesNotContain(second.Id, ids);
        Assert.DoesNotContain(third.Id, ids);
        Assert.Contains(control.Id, ids);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/posts/" + first.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/posts/" + second.Id)).StatusCode);
    }

    [Fact]
    public async Task Following_cursor_is_newest_first_stable_and_rejects_invalid_input()
    {
        var viewer = (await CreateUsersAsync(1))[0];
        var timestamp = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
        var ids = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        foreach (var id in ids)
        {
            await CreatePostAsync(viewer, PostPrivacy.ONLY_ME, timestamp, id);
        }

        using var client = CreateAuthenticatedClient(viewer);
        var invalid = await client.GetAsync("/api/feed/following?cursor=not-a-cursor");
        var first = await ReadAsync<FeedPageResponse>(
            await client.GetAsync("/api/feed/following?limit=2"));
        var second = await ReadAsync<FeedPageResponse>(
            await client.GetAsync("/api/feed/following?limit=2&cursor=" +
                Uri.EscapeDataString(first.NextCursor!)));
        var expected = ids.OrderByDescending(id => id).ToArray();
        var actual = first.Items.Concat(second.Items).Select(item => item.Id).ToArray();

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(expected, actual);
        Assert.Equal(actual.Length, actual.Distinct().Count());
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task Feed_items_batch_author_media_and_engagement_information()
    {
        var users = await CreateUsersAsync(2);
        var viewer = users[0];
        var author = users[1];
        await CreateFriendshipAsync(viewer, author);
        var post = await CreatePostAsync(author, PostPrivacy.FRIENDS, DateTimeOffset.UtcNow);
        var mediaId = await AttachReadyMediaAsync(author, post.Id);
        var now = DateTimeOffset.UtcNow;
        await AddCommentAsync(post.Id, author, now);
        await AddCommentAsync(post.Id, viewer, now.AddTicks(1));
        await AddReactionAsync(post.Id, author, ReactionType.LIKE, now);
        await AddReactionAsync(post.Id, viewer, ReactionType.LOVE, now.AddTicks(1));
        using var client = CreateAuthenticatedClient(viewer);

        var feed = await ReadAsync<FeedPageResponse>(await client.GetAsync("/api/feed"));
        var item = Assert.Single(feed.Items, item => item.Id == post.Id);
        var mediaAccess = await client.GetAsync(
            "/api/posts/" + post.Id + "/media/" + mediaId + "/access");

        Assert.Equal(author, item.Author.UserId);
        Assert.False(string.IsNullOrWhiteSpace(item.Author.DisplayName));
        Assert.Equal(2, item.CommentCount);
        Assert.Equal(2, item.ReactionCount);
        Assert.Equal(1, item.ReactionCounts["like"]);
        Assert.Equal(1, item.ReactionCounts["love"]);
        Assert.Equal("love", item.ViewerReaction);
        Assert.Contains(item.Media, media => media.MediaId == mediaId &&
            media.MediaType == "image" && media.ContentType == "image/png");
        Assert.Equal(HttpStatusCode.OK, mediaAccess.StatusCode);
    }

    [Fact]
    public async Task Following_traverses_multiple_large_pages_without_duplicate_posts()
    {
        var viewer = (await CreateUsersAsync(1))[0];
        var timestamp = DateTimeOffset.UtcNow;
        var expectedIds = new List<Guid>();
        for (var index = 0; index < 60; index++)
        {
            var post = await CreatePostAsync(
                viewer,
                PostPrivacy.ONLY_ME,
                timestamp.AddTicks(index));
            expectedIds.Add(post.Id);
        }

        using var client = CreateAuthenticatedClient(viewer);
        var first = await ReadAsync<FeedPageResponse>(
            await client.GetAsync("/api/feed/following?limit=50"));
        var second = await ReadAsync<FeedPageResponse>(
            await client.GetAsync("/api/feed/following?limit=50&cursor=" +
                Uri.EscapeDataString(first.NextCursor!)));
        var actual = first.Items.Concat(second.Items)
            .Where(item => expectedIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToArray();

        Assert.Equal(50, first.Items.Count);
        Assert.Equal(10, second.Items.Count);
        Assert.Equal(expectedIds.Count, actual.Distinct().Count());
        Assert.Equal(expectedIds.OrderByDescending(id => id), actual.OrderByDescending(id => id));
    }

    private async Task<Guid[]> CreateUsersAsync(int count)
    {
        var now = DateTimeOffset.UtcNow;
        var users = Enumerable.Range(0, count)
            .Select(index => new User(
                Guid.NewGuid(),
                "feed-" + Guid.NewGuid().ToString("N") + "@example.com",
                ("feed_" + Guid.NewGuid().ToString("N"))[..32],
                now.AddTicks(index)))
            .ToArray();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.Users.AddRange(users);
        db.UserProfiles.AddRange(users.Select(user =>
            new UserProfile(user.Id, user.UserName!, now)));
        await db.SaveChangesAsync();
        return users.Select(user => user.Id).ToArray();
    }

    private async Task<Post> CreatePostAsync(
        Guid authorUserId,
        PostPrivacy privacy,
        DateTimeOffset createdAtUtc,
        Guid? id = null)
    {
        var post = new Post(
            id ?? Guid.NewGuid(),
            authorUserId,
            "feed post " + Guid.NewGuid().ToString("N"),
            privacy,
            createdAtUtc);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.Posts.Add(post);
        await db.SaveChangesAsync();
        return post;
    }

    private async Task DeletePostAsync(Guid postId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var post = await db.Posts.SingleAsync(item => item.Id == postId);
        post.Delete(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
    }

    private async Task CreateFriendshipAsync(Guid firstUserId, Guid secondUserId)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<FriendsService>();
        var request = await service.SendRequestAsync(firstUserId, secondUserId);
        Assert.True(request.Succeeded);
        Assert.True((await service.AcceptRequestAsync(secondUserId, request.Value!.Id)).Succeeded);
    }

    private async Task BlockAsync(Guid blockerUserId, Guid blockedUserId)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<FriendsService>();
        Assert.True((await service.BlockAsync(blockerUserId, blockedUserId)).Succeeded);
    }

    private async Task UnfriendAsync(Guid firstUserId, Guid secondUserId)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<FriendsService>();
        Assert.True((await service.UnfriendAsync(firstUserId, secondUserId)).Succeeded);
    }

    private async Task<Guid> AttachReadyMediaAsync(Guid ownerUserId, Guid postId)
    {
        var now = DateTimeOffset.UtcNow;
        var mediaId = Guid.NewGuid();
        var asset = new MediaAsset(
            mediaId,
            ownerUserId,
            MediaType.IMAGE,
            ownerUserId.ToString("N") + "/" + mediaId.ToString("N") + ".png",
            "photo.png",
            "image/png",
            11,
            now,
            now.AddMinutes(5));
        asset.MarkReady(11, now);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.MediaAssets.Add(asset);
        db.PostMedia.Add(new PostMedia(postId, mediaId, 0));
        await db.SaveChangesAsync();
        return mediaId;
    }

    private async Task AddCommentAsync(Guid postId, Guid authorUserId, DateTimeOffset createdAtUtc)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.Comments.Add(new Comment(
            Guid.NewGuid(),
            postId,
            authorUserId,
            null,
            "feed comment",
            createdAtUtc));
        await db.SaveChangesAsync();
    }

    private async Task AddReactionAsync(
        Guid postId,
        Guid userId,
        ReactionType type,
        DateTimeOffset createdAtUtc)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.PostReactions.Add(new PostReaction(postId, userId, type, createdAtUtc));
        await db.SaveChangesAsync();
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
