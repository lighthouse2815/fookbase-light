using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Reels.DTOs.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class ReelEndpointsTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    [Fact]
    public async Task Reel_creation_requires_processed_owned_video_and_keeps_legacy_posts_standard()
    {
        var users = await CreateUsersAsync(2);
        var ownerId = users[0];
        var otherId = users[1];
        var imageId = await CreateReadyMediaAsync(ownerId, MediaType.IMAGE);
        var pendingVideoId = await CreatePendingVideoAsync(ownerId);
        var foreignVideoId = await CreateReadyMediaAsync(otherId, MediaType.VIDEO);
        var ownedVideoId = await CreateReadyMediaAsync(ownerId, MediaType.VIDEO);
        using var anonymous = factory.CreateClient();
        using var owner = CreateAuthenticatedClient(ownerId);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/reels",
            new { caption = "", privacy = "public", videoMediaId = ownedVideoId })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync("/api/reels",
            new { caption = "image", privacy = "public", videoMediaId = imageId })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync("/api/reels",
            new { caption = "pending", privacy = "public", videoMediaId = pendingVideoId })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync("/api/reels",
            new { caption = "foreign", privacy = "public", videoMediaId = foreignVideoId })).StatusCode);

        var created = await ReadAsync<ReelResponse>(await owner.PostAsJsonAsync("/api/reels",
            new { caption = "", privacy = "public", videoMediaId = ownedVideoId }));
        var standard = await owner.PostAsJsonAsync("/api/posts",
            new { content = "legacy standard", privacy = "public" });
        standard.EnsureSuccessStatusCode();

        Assert.Empty(created.Caption);
        Assert.Equal(ownedVideoId, created.Video.MediaId);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Equal(PostType.REEL, (await db.Posts.SingleAsync(post => post.Id == created.Id)).PostType);
        Assert.Equal(1, await db.PostMedia.CountAsync(item => item.PostId == created.Id));
        Assert.True(await db.MediaReferences.AnyAsync(item =>
            item.PostId == created.Id && item.MediaId == ownedVideoId));
        Assert.True(await db.Posts.AnyAsync(post =>
            post.Content == "legacy standard" && post.PostType == PostType.STANDARD));
        Assert.Equal(HttpStatusCode.Conflict, (await owner.DeleteAsync("/api/media/" + ownedVideoId)).StatusCode);
    }

    [Fact]
    public async Task Reels_feed_media_access_views_and_post_interactions_follow_visibility_rules()
    {
        var users = await CreateUsersAsync(3);
        var authorId = users[0];
        var friendId = users[1];
        var strangerId = users[2];
        await CreateFriendshipAsync(authorId, friendId);
        using var author = CreateAuthenticatedClient(authorId);
        using var friend = CreateAuthenticatedClient(friendId);
        using var stranger = CreateAuthenticatedClient(strangerId);
        var publicVideo = await CreateReadyMediaAsync(authorId, MediaType.VIDEO);
        var friendsVideo = await CreateReadyMediaAsync(authorId, MediaType.VIDEO);
        var onlyMeVideo = await CreateReadyMediaAsync(authorId, MediaType.VIDEO);
        var publicReel = await ReadAsync<ReelResponse>(await author.PostAsJsonAsync("/api/reels",
            new { caption = "public reel", privacy = "public", videoMediaId = publicVideo }));
        var friendsReel = await ReadAsync<ReelResponse>(await author.PostAsJsonAsync("/api/reels",
            new { caption = "friends reel", privacy = "friends", videoMediaId = friendsVideo }));
        var onlyMeReel = await ReadAsync<ReelResponse>(await author.PostAsJsonAsync("/api/reels",
            new { caption = "private reel", privacy = "onlyMe", videoMediaId = onlyMeVideo }));

        var friendFeed = await ReadAsync<ReelPageResponse>(await friend.GetAsync("/api/reels?limit=2"));
        var friendSecond = friendFeed.NextCursor is null
            ? new ReelPageResponse([], null)
            : await ReadAsync<ReelPageResponse>(await friend.GetAsync("/api/reels?limit=2&cursor=" +
                Uri.EscapeDataString(friendFeed.NextCursor)));
        var strangerFeed = await ReadAsync<ReelPageResponse>(await stranger.GetAsync("/api/reels"));
        var accessibleFriendIds = friendFeed.Items.Concat(friendSecond.Items).Select(item => item.Id).ToArray();

        Assert.Contains(publicReel.Id, accessibleFriendIds);
        Assert.Contains(friendsReel.Id, accessibleFriendIds);
        Assert.DoesNotContain(onlyMeReel.Id, accessibleFriendIds);
        Assert.Contains(strangerFeed.Items, item => item.Id == publicReel.Id);
        Assert.DoesNotContain(strangerFeed.Items, item => item.Id == friendsReel.Id || item.Id == onlyMeReel.Id);

        var videoAccess = await stranger.GetAsync($"/api/reels/{publicReel.Id}/video/access");
        var posterAccess = await stranger.GetAsync($"/api/reels/{publicReel.Id}/poster/access");
        var reacted = await stranger.PutAsJsonAsync($"/api/posts/{publicReel.Id}/reaction", new { type = "love" });
        var commented = await stranger.PostAsJsonAsync($"/api/posts/{publicReel.Id}/comments",
            new { content = "great reel", parentCommentId = (Guid?)null });
        var view = await stranger.PostAsJsonAsync($"/api/reels/{publicReel.Id}/views",
            new { watchDurationMs = 10_000, completed = true, replayed = false });
        var invalidView = await stranger.PostAsJsonAsync($"/api/reels/{publicReel.Id}/views",
            new { watchDurationMs = 10_001, completed = false, replayed = false });

        Assert.Equal(HttpStatusCode.OK, videoAccess.StatusCode);
        Assert.Equal(HttpStatusCode.OK, posterAccess.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reacted.StatusCode);
        Assert.Equal(HttpStatusCode.Created, commented.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, view.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidView.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            Assert.Single(await db.ReelViews.Where(item => item.ReelPostId == publicReel.Id).ToListAsync());
            Assert.True(await db.PostReactions.AnyAsync(item => item.PostId == publicReel.Id && item.UserId == strangerId));
            Assert.True(await db.Comments.AnyAsync(item => item.PostId == publicReel.Id && item.AuthorUserId == strangerId));
        }

        await BlockAsync(authorId, friendId);
        Assert.Equal(HttpStatusCode.NotFound, (await friend.GetAsync("/api/reels/" + publicReel.Id)).StatusCode);
        var blockedFeed = await ReadAsync<ReelPageResponse>(await friend.GetAsync("/api/reels"));
        Assert.DoesNotContain(blockedFeed.Items, item => item.Author.UserId == authorId);
    }

    [Fact]
    public async Task Reel_author_does_not_expose_email_when_profile_is_missing()
    {
        var email = $"reel-private-{Guid.NewGuid():N}@example.com";
        var authorId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            db.Users.Add(new User(authorId, email, email, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        using var author = CreateAuthenticatedClient(authorId);
        var videoId = await CreateReadyMediaAsync(authorId, MediaType.VIDEO);
        var created = await ReadAsync<ReelResponse>(await author.PostAsJsonAsync("/api/reels",
            new { caption = "private contact", privacy = "public", videoMediaId = videoId }));
        var loaded = await ReadAsync<ReelResponse>(await author.GetAsync("/api/reels/" + created.Id));

        Assert.Empty(created.Author.Username);
        Assert.Equal("Người dùng", created.Author.DisplayName);
        Assert.DoesNotContain(email, await author.GetStringAsync("/api/reels/" + created.Id));
        Assert.Empty(loaded.Author.Username);
        Assert.Equal("Người dùng", loaded.Author.DisplayName);
    }

    private async Task<Guid[]> CreateUsersAsync(int count)
    {
        var users = Enumerable.Range(0, count)
            .Select(index => new User(
                Guid.NewGuid(),
                $"reels-{Guid.NewGuid():N}@example.com",
                $"reels_{Guid.NewGuid():N}"[..32],
                DateTimeOffset.UtcNow.AddTicks(index)))
            .ToArray();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.Users.AddRange(users);
        await db.SaveChangesAsync();
        return users.Select(user => user.Id).ToArray();
    }

    private async Task<Guid> CreatePendingVideoAsync(Guid ownerUserId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.MediaAssets.Add(MediaAsset.CreatePending(id, ownerUserId, MediaType.VIDEO,
            $"{ownerUserId:N}/{id:N}.mp4", "pending.mp4", "video/mp4", 11, now, now.AddMinutes(5)));
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> CreateReadyMediaAsync(Guid ownerUserId, MediaType mediaType)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var asset = MediaAsset.CreatePending(id, ownerUserId, mediaType,
            $"{ownerUserId:N}/{id:N}{(mediaType == MediaType.VIDEO ? ".mp4" : ".png")}",
            mediaType == MediaType.VIDEO ? "video.mp4" : "image.png",
            mediaType == MediaType.VIDEO ? "video/mp4" : "image/png", 11, now, now.AddMinutes(5));
        if (mediaType == MediaType.VIDEO)
        {
            asset.MarkProcessing(11, now);
            asset.MarkVideoReady(MediaAsset.ProcessedKey(ownerUserId, id), MediaAsset.PosterKey(ownerUserId, id),
                10_000, 720, 1280, now);
        }
        else
        {
            asset.MarkReady(11, now);
        }

        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync();
        return id;
    }

    private async Task CreateFriendshipAsync(Guid senderUserId, Guid receiverUserId)
    {
        using var scope = factory.Services.CreateScope();
        var friends = scope.ServiceProvider.GetRequiredService<FriendsService>();
        var request = await friends.SendRequestAsync(senderUserId, receiverUserId);
        Assert.True(request.Succeeded);
        Assert.True((await friends.AcceptRequestAsync(receiverUserId, request.Value!.Id)).Succeeded);
    }

    private async Task BlockAsync(Guid blockerUserId, Guid blockedUserId)
    {
        using var scope = factory.Services.CreateScope();
        var friends = scope.ServiceProvider.GetRequiredService<FriendsService>();
        Assert.True((await friends.BlockAsync(blockerUserId, blockedUserId)).Succeeded);
    }

    private HttpClient CreateAuthenticatedClient(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"],
            [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())], now.AddSeconds(-1), now.AddMinutes(5),
            new SigningCredentials(new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new InvalidOperationException("Response body was empty.");
    }
}
