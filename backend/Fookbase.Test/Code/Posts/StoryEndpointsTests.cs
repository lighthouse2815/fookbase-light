using Fookbase.Api.Modules.Media.Domain.Enums;
using Fookbase.Api.Modules.Notifications.Domain.Enums;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Messages.Services;
using Fookbase.Api.Modules.Notifications.Entities;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Modules.Stories.Domain.Enums;
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
    public async Task Story_routes_use_controllers_and_require_authorization()
    {
        using var client = factory.CreateClient();
        var routes = new (string Method, string Path)[]
        {
            ("GET", "api/stories"),
            ("POST", "api/stories"),
            ("GET", "api/stories/archive"),
            ("GET", "api/stories/{storyId:guid}"),
            ("DELETE", "api/stories/{storyId:guid}"),
            ("POST", "api/stories/{storyId:guid}/view"),
            ("GET", "api/stories/{storyId:guid}/viewers"),
            ("POST", "api/stories/{storyId:guid}/reaction"),
            ("DELETE", "api/stories/{storyId:guid}/reaction"),
            ("POST", "api/stories/{storyId:guid}/reply"),
            ("GET", "api/stories/{storyId:guid}/media/access"),
            ("GET", "api/stories/{storyId:guid}/media/poster/access")
        };
        var endpoints = factory.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>()
            .Endpoints.OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.TrimStart('/').StartsWith("api/stories") == true)
            .ToArray();
        Assert.Equal(routes.Length, endpoints.Length);
        foreach (var route in routes)
        {
            var endpoint = Assert.Single(endpoints, item =>
                item.RoutePattern.RawText!.TrimStart('/') == route.Path &&
                item.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!
                    .HttpMethods.Contains(route.Method));
            Assert.NotNull(endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>());
            Assert.NotNull(endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>());
            using var request = new HttpRequestMessage(new HttpMethod(route.Method),
                "/" + route.Path.Replace("{storyId:guid}", Guid.NewGuid().ToString()));
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("", "Privacy", "Quyền riêng tư của story là bắt buộc.")]
    [InlineData("/reply", "Content", "Nội dung trả lời story là bắt buộc.")]
    [InlineData("/reaction", "Type", "Loại cảm xúc là bắt buộc.")]
    public async Task Story_requests_reject_missing_or_blank_required_fields(
        string suffix, string field, string message)
    {
        using var client = CreateAuthenticatedClient((await CreateUsersAsync(1))[0]);
        var path = suffix.Length == 0 ? "/api/stories" : $"/api/stories/{Guid.NewGuid()}{suffix}";
        foreach (var value in new string?[] { null, string.Empty, "   " })
        {
            var request = new Dictionary<string, object?>
            {
                ["MediaId"] = Guid.NewGuid(),
                ["Privacy"] = "public",
                ["Content"] = "A reply",
                ["Type"] = "like"
            };
            if (value is null) request.Remove(field);
            else request[field] = value;
            using var response = await client.PostAsJsonAsync(path, request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("validation_failed", body.RootElement.GetProperty("code").GetString());
            Assert.Equal(message, Assert.Single(body.RootElement.GetProperty("errors")
                .GetProperty(field).EnumerateArray()).GetString());
        }
    }

    [Fact]
    public async Task Story_creation_requires_a_non_empty_media_id()
    {
        using var client = CreateAuthenticatedClient((await CreateUsersAsync(1))[0]);
        foreach (var omitted in new[] { true, false })
        {
            var request = new Dictionary<string, object?> { ["Privacy"] = "public" };
            if (!omitted) request["MediaId"] = Guid.Empty;
            using var response = await client.PostAsJsonAsync("/api/stories", request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("Media là bắt buộc.", Assert.Single(body.RootElement.GetProperty("errors")
                .GetProperty("MediaId").EnumerateArray()).GetString());
        }
    }

    [Fact]
    public async Task Story_text_limits_use_trimmed_length_and_validate_before_writing()
    {
        var users = await CreateUsersAsync(2);
        var mediaId = await CreateReadyMediaAsync(users[0], MediaType.IMAGE);
        await CreateFriendshipAsync(users[0], users[1]);
        using var owner = CreateAuthenticatedClient(users[0]);
        using var friend = CreateAuthenticatedClient(users[1]);
        using var invalidCaption = await owner.PostAsJsonAsync("/api/stories", new
        {
            mediaId,
            caption = " " + new string('a', Story.MaximumCaptionLength + 1) + " ",
            privacy = "public"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidCaption.StatusCode);
        using (var body = JsonDocument.Parse(await invalidCaption.Content.ReadAsStringAsync()))
        {
            Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("Caption", out _));
        }
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            Assert.False(await db.Stories.AnyAsync(item => item.AuthorUserId == users[0]));
        }

        var story = await CreateStoryAsync(owner, mediaId,
            " " + new string('a', Story.MaximumCaptionLength) + " ", " PUBLIC ");
        Assert.Equal(new string('a', Story.MaximumCaptionLength), story.Caption);
        using var invalidReply = await friend.PostAsJsonAsync($"/api/stories/{story.Id}/reply", new
        {
            content = " " + new string('a', MessagesService.MaximumContentLength + 1) + " "
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidReply.StatusCode);
        using (var body = JsonDocument.Parse(await invalidReply.Content.ReadAsStringAsync()))
        {
            Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("Content", out _));
        }
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            Assert.False(await db.Messages.AnyAsync(item => item.StoryId == story.Id));
        }

        using var validReply = await friend.PostAsJsonAsync($"/api/stories/{story.Id}/reply", new
        {
            content = " " + new string('a', MessagesService.MaximumContentLength) + " "
        });
        Assert.Equal(HttpStatusCode.Created, validReply.StatusCode);
        var reply = await ReadAsync<Fookbase.Api.Modules.Messages.DTOs.Responses.MessageResponse>(validReply);
        Assert.Equal(new string('a', MessagesService.MaximumContentLength), reply.Content);
    }

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

    [Fact]
    public async Task Story_relationships_load_the_graph_and_cascade_only_story_dependents()
    {
        var users = await CreateUsersAsync(2);
        var mediaId = await CreateReadyMediaAsync(users[0], MediaType.IMAGE);
        var now = DateTimeOffset.UtcNow;
        var storyId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            db.Stories.Add(new Story(storyId, users[0], mediaId, "mapped story", PostPrivacy.PUBLIC,
                now, now.AddHours(24)));
            db.StoryMediaReferences.Add(new StoryMediaReference(storyId, mediaId, now));
            db.StoryViews.Add(new StoryView(storyId, users[1], now));
            db.StoryReactions.Add(new StoryReaction(storyId, users[1], StoryReactionType.LOVE, now));
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            var story = await db.Stories
                .Include(item => item.AuthorUser)
                .Include(item => item.Media)
                .Include(item => item.MediaReference).ThenInclude(item => item!.Media)
                .Include(item => item.Views).ThenInclude(item => item.ViewerUser)
                .Include(item => item.Reactions).ThenInclude(item => item.User)
                .AsSplitQuery()
                .SingleAsync(item => item.Id == storyId);
            Assert.Equal(users[0], story.AuthorUser.Id);
            Assert.Equal(mediaId, story.Media.Id);
            Assert.Equal(mediaId, story.MediaReference!.Media.Id);
            Assert.Same(story, story.MediaReference.Story);
            Assert.Equal(users[1], Assert.Single(story.Views).ViewerUser.Id);
            Assert.Equal(users[1], Assert.Single(story.Reactions).User.Id);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            var authorDelete = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
                db.Users.Where(item => item.Id == users[0]).ExecuteDeleteAsync());
            var viewerDelete = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
                db.Users.Where(item => item.Id == users[1]).ExecuteDeleteAsync());
            var mediaDelete = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
                db.MediaAssets.Where(item => item.Id == mediaId).ExecuteDeleteAsync());
            Assert.Equal(Npgsql.PostgresErrorCodes.ForeignKeyViolation, authorDelete.SqlState);
            Assert.Equal(Npgsql.PostgresErrorCodes.ForeignKeyViolation, viewerDelete.SqlState);
            Assert.Equal(Npgsql.PostgresErrorCodes.ForeignKeyViolation, mediaDelete.SqlState);
        }

        // Delete without loading dependents to verify the database cascade, not EF fixup.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            await db.Stories.Where(item => item.Id == storyId).ExecuteDeleteAsync();
        }
        using var afterDelete = factory.Services.CreateScope();
        var afterDeleteDb = afterDelete.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.False(await afterDeleteDb.StoryMediaReferences.AnyAsync(item => item.StoryId == storyId));
        Assert.False(await afterDeleteDb.StoryViews.AnyAsync(item => item.StoryId == storyId));
        Assert.False(await afterDeleteDb.StoryReactions.AnyAsync(item => item.StoryId == storyId));
        Assert.Equal(2, await afterDeleteDb.Users.CountAsync(item => users.Contains(item.Id)));
        Assert.True(await afterDeleteDb.MediaAssets.AnyAsync(item => item.Id == mediaId));
    }

    private async Task<StoryResponse> CreateStoryAsync(HttpClient client, Guid mediaId, string caption, string privacy) =>
        await ReadAsync<StoryResponse>(await client.PostAsJsonAsync("/api/stories",
            new { mediaId, caption, privacy }));

    private async Task<Guid> CreateExpiredStoryAsync(Guid ownerUserId)
    {
        var mediaId = await CreateReadyMediaAsync(ownerUserId, MediaType.IMAGE);
        var now = DateTimeOffset.UtcNow;
        var story = new Story(Guid.NewGuid(), ownerUserId, mediaId, "expired", PostPrivacy.PUBLIC,
            now.AddDays(-2), now.AddHours(-1));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.Stories.Add(story);
        db.StoryMediaReferences.Add(new StoryMediaReference(story.Id, mediaId, now.AddDays(-2)));
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
        db.MediaAssets.Add(new MediaAsset(id, ownerUserId, MediaType.VIDEO,
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
        var asset = new MediaAsset(id, ownerUserId, type,
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
