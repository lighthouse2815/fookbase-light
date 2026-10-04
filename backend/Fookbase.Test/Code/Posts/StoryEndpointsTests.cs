using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Notifications.Entities;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Modules.Stories.DTOs.Responses;
using Fookbase.Api.Modules.Stories.Entities;
using Fookbase.Api.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class StoryEndpointsTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    [Fact]
    public async Task Story_creation_validates_media_and_releases_its_media_reference_when_deleted()
    {
        var users = await CreateUsersAsync(2);
        var ownerId = users[0];
        var otherId = users[1];
        var imageId = await CreateReadyMediaAsync(ownerId, MediaType.IMAGE);
        var processedVideoId = await CreateReadyMediaAsync(ownerId, MediaType.VIDEO);
        var pendingVideoId = await CreatePendingVideoAsync(ownerId);
        var tooLongVideoId = await CreateReadyMediaAsync(ownerId, MediaType.VIDEO, 60_001);
        var foreignImageId = await CreateReadyMediaAsync(otherId, MediaType.IMAGE);
        using var owner = CreateAuthenticatedClient(ownerId);
        using var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/stories",
            new { mediaId = imageId, caption = "hello", privacy = "public" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync("/api/stories",
            new { mediaId = pendingVideoId, caption = "pending", privacy = "public" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/stories",
            new { mediaId = tooLongVideoId, caption = "long", privacy = "public" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync("/api/stories",
            new { mediaId = foreignImageId, caption = "foreign", privacy = "public" })).StatusCode);

        var imageStory = await ReadAsync<StoryResponse>(await owner.PostAsJsonAsync("/api/stories",
            new { mediaId = imageId, caption = "image story", privacy = "onlyMe" }));
        var videoStory = await ReadAsync<StoryResponse>(await owner.PostAsJsonAsync("/api/stories",
            new { mediaId = processedVideoId, caption = "video story", privacy = "friends" }));

        Assert.Equal("image", imageStory.Media.MediaType);
        Assert.Equal("video", videoStory.Media.MediaType);
        Assert.Equal("onlyMe", imageStory.Privacy);
        Assert.InRange(imageStory.ExpiresAtUtc - imageStory.CreatedAtUtc, TimeSpan.FromHours(23.99), TimeSpan.FromHours(24.01));
        Assert.Equal(HttpStatusCode.Conflict, (await owner.DeleteAsync($"/api/media/{imageId}")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            Assert.True(await db.StoryMediaReferences.AnyAsync(reference =>
                reference.StoryId == imageStory.Id && reference.MediaId == imageId));
            Assert.True(await db.StoryMediaReferences.AnyAsync(reference =>
                reference.StoryId == videoStory.Id && reference.MediaId == processedVideoId));
        }

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/stories/{imageStory.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/media/{imageId}")).StatusCode);
        using var afterDelete = factory.Services.CreateScope();
        var afterDeleteDb = afterDelete.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.False(await afterDeleteDb.StoryMediaReferences.AnyAsync(reference => reference.StoryId == imageStory.Id));
    }

    [Fact]
    public async Task Story_responses_include_the_uploaded_avatar_path()
    {
        var ownerId = (await CreateUsersAsync(1))[0];
        var avatarMediaId = await CreateReadyMediaAsync(ownerId, MediaType.IMAGE);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            var profile = new UserProfile(ownerId, "story_author", DateTimeOffset.UtcNow);
            profile.Update("Story Author", null, null, null, avatarMediaId, null, DateTimeOffset.UtcNow);
            db.UserProfiles.Add(profile);
            await db.SaveChangesAsync();
        }

        using var owner = CreateAuthenticatedClient(ownerId);
        var story = await CreateStoryAsync(owner, await CreateReadyMediaAsync(ownerId, MediaType.IMAGE), "avatar", "public");
        var tray = await ReadAsync<StoryTrayResponse>(await owner.GetAsync("/api/stories"));

        Assert.Equal($"/api/users/{ownerId}/avatar", story.Author.AvatarUrl);
        Assert.Equal($"/api/users/{ownerId}/avatar", tray.Items.Single(item => item.Author.UserId == ownerId).Author.AvatarUrl);
    }

    [Fact]
    public async Task Story_tray_views_viewers_and_archive_enforce_privacy_expiration_and_blocks()
    {
        var users = await CreateUsersAsync(4);
        var ownerId = users[0];
        var friendId = users[1];
        var strangerId = users[2];
        var blockedId = users[3];
        await CreateFriendshipAsync(ownerId, friendId);
        await CreateFriendshipAsync(ownerId, blockedId);
        using var owner = CreateAuthenticatedClient(ownerId);
        using var friend = CreateAuthenticatedClient(friendId);
        using var stranger = CreateAuthenticatedClient(strangerId);
        using var blocked = CreateAuthenticatedClient(blockedId);
        var publicStory = await CreateStoryAsync(owner, await CreateReadyMediaAsync(ownerId, MediaType.IMAGE), "public", "public");
        var friendsStory = await CreateStoryAsync(owner, await CreateReadyMediaAsync(ownerId, MediaType.IMAGE), "friends", "friends");
        var privateStory = await CreateStoryAsync(owner, await CreateReadyMediaAsync(ownerId, MediaType.IMAGE), "private", "onlyMe");

        var ownTray = await ReadAsync<StoryTrayResponse>(await owner.GetAsync("/api/stories"));
        var friendTray = await ReadAsync<StoryTrayResponse>(await friend.GetAsync("/api/stories"));
        var strangerTray = await ReadAsync<StoryTrayResponse>(await stranger.GetAsync("/api/stories"));
        Assert.Contains(ownTray.Items, item => item.Author.UserId == ownerId && item.Stories.Count >= 3);
        var friendStories = friendTray.Items.Single(item => item.Author.UserId == ownerId).Stories;
        Assert.Contains(friendStories, story => story.Id == publicStory.Id);
        Assert.Contains(friendStories, story => story.Id == friendsStory.Id);
        Assert.DoesNotContain(friendStories, story => story.Id == privateStory.Id);
        Assert.DoesNotContain(strangerTray.Items, item => item.Author.UserId == ownerId);

        Assert.Equal(HttpStatusCode.NoContent, (await friend.PostAsync($"/api/stories/{publicStory.Id}/view", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await friend.PostAsync($"/api/stories/{publicStory.Id}/view", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"/api/stories/{publicStory.Id}/view", null)).StatusCode);
        var viewers = await ReadAsync<StoryViewersPageResponse>(await owner.GetAsync(
            $"/api/stories/{publicStory.Id}/viewers?limit=10"));
        Assert.Single(viewers.Items);
        Assert.Equal(friendId, viewers.Items[0].UserId);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync($"/api/stories/{publicStory.Id}/viewers")).StatusCode);

        var expiredStoryId = await CreateExpiredStoryAsync(ownerId);
        Assert.Equal(HttpStatusCode.NotFound, (await friend.GetAsync($"/api/stories/{expiredStoryId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await friend.PostAsJsonAsync(
            $"/api/stories/{expiredStoryId}/reply", new { content = "too late" })).StatusCode);
        var archive = await ReadAsync<StoryArchivePageResponse>(await owner.GetAsync("/api/stories/archive?limit=10"));
        Assert.NotEmpty(archive.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await friend.GetAsync($"/api/stories/{archive.Items[0].Id}")).StatusCode);

        await BlockAsync(ownerId, blockedId);
        Assert.DoesNotContain((await ReadAsync<StoryTrayResponse>(await blocked.GetAsync("/api/stories"))).Items,
            item => item.Author.UserId == ownerId);
        Assert.Equal(HttpStatusCode.NotFound, (await blocked.GetAsync($"/api/stories/{publicStory.Id}")).StatusCode);
    }

    [Fact]
    public async Task Story_reactions_and_replies_use_notification_and_existing_direct_message_policy()
    {
        var users = await CreateUsersAsync(3);
        var ownerId = users[0];
        var friendId = users[1];
        var strangerId = users[2];
        await CreateFriendshipAsync(ownerId, friendId);
        using var owner = CreateAuthenticatedClient(ownerId);
        using var friend = CreateAuthenticatedClient(friendId);
        using var stranger = CreateAuthenticatedClient(strangerId);
        var story = await CreateStoryAsync(owner, await CreateReadyMediaAsync(ownerId, MediaType.IMAGE), "react", "public");

        var added = await ReadAsync<StoryResponse>(await friend.PostAsJsonAsync(
            $"/api/stories/{story.Id}/reaction", new { type = "like" }));
        var changed = await ReadAsync<StoryResponse>(await friend.PostAsJsonAsync(
            $"/api/stories/{story.Id}/reaction", new { type = "love" }));
        Assert.Equal("like", added.ViewerReaction);
        Assert.Equal("love", changed.ViewerReaction);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(
            $"/api/stories/{story.Id}/reaction", new { type = "like" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await friend.DeleteAsync($"/api/stories/{story.Id}/reaction")).StatusCode);

        var reply = await friend.PostAsJsonAsync($"/api/stories/{story.Id}/reply",
            new { content = "A story reply" });
        var message = await ReadAsync<Fookbase.Api.Modules.Messages.DTOs.Responses.MessageResponse>(reply);
        Assert.Equal(story.Id, message.Story?.StoryId);
        Assert.True(message.Story?.IsAvailable);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.PostAsJsonAsync(
            $"/api/stories/{story.Id}/reply", new { content = "not friends" })).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            Assert.Empty(await db.StoryReactions.Where(item => item.StoryId == story.Id).ToListAsync());
            Assert.Contains(await db.Notifications.ToListAsync(), notification =>
                notification.Type == NotificationType.STORY_REACTION &&
                notification.RecipientUserId == ownerId &&
                notification.ActorUserId == friendId &&
                notification.EntityId == story.Id);
            Assert.DoesNotContain(await db.Notifications.ToListAsync(), notification =>
                notification.Type == NotificationType.STORY_REACTION && notification.ActorUserId == ownerId);
            Assert.True(await db.Messages.AnyAsync(item => item.Id == message.Id && item.StoryId == story.Id));
        }

        await BlockAsync(ownerId, friendId);
        Assert.Equal(HttpStatusCode.NotFound, (await friend.PostAsJsonAsync(
            $"/api/stories/{story.Id}/reply", new { content = "blocked" })).StatusCode);
    }

    private async Task<StoryResponse> CreateStoryAsync(HttpClient client, Guid mediaId, string caption, string privacy) =>
        await ReadAsync<StoryResponse>(await client.PostAsJsonAsync("/api/stories",
            new { mediaId, caption, privacy }));

    private async Task<Guid> CreateExpiredStoryAsync(Guid ownerUserId)
    {
        var mediaId = await CreateReadyMediaAsync(ownerUserId, MediaType.IMAGE);
        var now = DateTimeOffset.UtcNow;
        var story = Story.Create(Guid.NewGuid(), ownerUserId, mediaId, "expired", PostPrivacy.PUBLIC,
            now.AddDays(-2), now.AddHours(-1));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.Stories.Add(story);
        db.StoryMediaReferences.Add(StoryMediaReference.Create(story.Id, mediaId, now.AddDays(-2)));
        await db.SaveChangesAsync();
        return story.Id;
    }

    private async Task<Guid[]> CreateUsersAsync(int count)
    {
        var users = Enumerable.Range(0, count).Select(index => new User(
            Guid.NewGuid(),
            $"stories-{Guid.NewGuid():N}@example.com",
            $"stories_{Guid.NewGuid():N}"[..32],
            DateTimeOffset.UtcNow.AddTicks(index))).ToArray();
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

    private async Task<Guid> CreateReadyMediaAsync(Guid ownerUserId, MediaType type, long durationMs = 10_000)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var asset = MediaAsset.CreatePending(id, ownerUserId, type,
            $"{ownerUserId:N}/{id:N}{(type == MediaType.VIDEO ? ".mp4" : ".png")}",
            type == MediaType.VIDEO ? "video.mp4" : "image.png",
            type == MediaType.VIDEO ? "video/mp4" : "image/png", 11, now, now.AddMinutes(5));
        if (type == MediaType.VIDEO)
        {
            asset.MarkProcessing(11, now);
            asset.MarkVideoReady(MediaAsset.ProcessedKey(ownerUserId, id), MediaAsset.PosterKey(ownerUserId, id),
                durationMs, 720, 1280, now);
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
