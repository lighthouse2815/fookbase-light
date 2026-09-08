using Fookbase.Api.Modules.Posts.DTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Fookbase.Api.Shared.Contracts.Friends;
using Fookbase.Api.Shared.Contracts.Identity;
using Fookbase.Api.Shared.Contracts.Media;
using Fookbase.Api.Shared.Contracts.Posts;
using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Modules.Posts.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class PostEndpointsTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    [Fact]
    public async Task Mutation_without_jwt_returns_unauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/posts",
            new { content = "hello", privacy = "public" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Author_can_create_update_and_soft_delete_post()
    {
        var users = await CreateKnownUsersAsync(2);
        using var author = CreateAuthenticatedClient(users[0]);
        using var other = CreateAuthenticatedClient(users[1]);

        var invalid = await author.PostAsJsonAsync(
            "/api/posts",
            new { content = "   ", privacy = "public" });
        var createdResponse = await author.PostAsJsonAsync(
            "/api/posts",
            new { content = "  first post  ", privacy = "friends" });
        var created = await ReadAsync<PostResponse>(createdResponse);
        var forbidden = await other.PutAsJsonAsync(
            $"/api/posts/{created.Id}",
            new { content = "hijacked", privacy = "public" });
        var updatedResponse = await author.PutAsJsonAsync(
            $"/api/posts/{created.Id}",
            new { content = "updated", privacy = "onlyMe" });
        var updated = await ReadAsync<PostResponse>(updatedResponse);
        var deleted = await author.DeleteAsync($"/api/posts/{created.Id}");
        var missing = await author.GetAsync($"/api/posts/{created.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.Equal("first post", created.Content);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("updated", updated.Content);
        Assert.Equal("onlyMe", updated.Privacy);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PostsDbContext>();
        Assert.NotNull((await dbContext.Posts.AsNoTracking().SingleAsync(post => post.Id == created.Id)).DeletedAtUtc);
        Assert.Contains(await dbContext.OutboxMessages.AsNoTracking().ToListAsync(),
            message => message.Type == PostCreatedIntegrationEvent.EventType);
        Assert.Contains(await dbContext.OutboxMessages.AsNoTracking().ToListAsync(),
            message => message.Type == PostUpdatedIntegrationEvent.EventType);
        Assert.Contains(await dbContext.OutboxMessages.AsNoTracking().ToListAsync(),
            message => message.Type == PostDeletedIntegrationEvent.EventType);
    }

    [Fact]
    public async Task Privacy_feed_and_block_projection_control_visibility()
    {
        var users = await CreateKnownUsersAsync(3);
        var authorId = users[0];
        var friendId = users[1];
        var strangerId = users[2];
        await ProjectAsync(new FriendRequestAcceptedIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Min(authorId, friendId), Max(authorId, friendId), DateTimeOffset.UtcNow));
        using var author = CreateAuthenticatedClient(authorId);
        using var friend = CreateAuthenticatedClient(friendId);
        using var stranger = CreateAuthenticatedClient(strangerId);
        using var anonymous = factory.CreateClient();

        var publicPost = await CreatePostAsync(author, "public post", "public");
        var friendsPost = await CreatePostAsync(author, "friends post", "friends");
        var privatePost = await CreatePostAsync(author, "private post", "onlyMe");

        var anonymousPosts = await ReadAsync<PagedResponse<PostResponse>>(
            await anonymous.GetAsync($"/api/posts/users/{authorId}"));
        var friendFeed = await ReadAsync<PagedResponse<PostResponse>>(
            await friend.GetAsync("/api/posts/feed"));
        var strangerFeed = await ReadAsync<PagedResponse<PostResponse>>(
            await stranger.GetAsync("/api/posts/feed"));

        Assert.Single(anonymousPosts.Items);
        Assert.Equal(publicPost.Id, anonymousPosts.Items[0].Id);
        Assert.Contains(friendFeed.Items, item => item.Id == publicPost.Id);
        Assert.Contains(friendFeed.Items, item => item.Id == friendsPost.Id);
        Assert.DoesNotContain(friendFeed.Items, item => item.Id == privatePost.Id);
        Assert.Contains(strangerFeed.Items, item => item.Id == publicPost.Id);
        Assert.DoesNotContain(strangerFeed.Items, item => item.Id == friendsPost.Id);
        Assert.DoesNotContain(strangerFeed.Items, item => item.Id == privatePost.Id);

        await ProjectAsync(new UserBlockedIntegrationEvent(
            Guid.NewGuid(), authorId, friendId, DateTimeOffset.UtcNow.AddSeconds(1)));
        var blockedFeed = await ReadAsync<PagedResponse<PostResponse>>(
            await friend.GetAsync("/api/posts/feed"));
        var blockedDirect = await friend.GetAsync($"/api/posts/{friendsPost.Id}");

        Assert.DoesNotContain(blockedFeed.Items, item => item.AuthorUserId == authorId);
        Assert.Equal(HttpStatusCode.NotFound, blockedDirect.StatusCode);
    }

    [Fact]
    public async Task Comments_and_reactions_enforce_access_and_return_summaries()
    {
        var users = await CreateKnownUsersAsync(2);
        using var author = CreateAuthenticatedClient(users[0]);
        using var reader = CreateAuthenticatedClient(users[1]);
        var post = await CreatePostAsync(author, "discussion", "public");

        var commentResponse = await reader.PostAsJsonAsync(
            $"/api/posts/{post.Id}/comments",
            new { content = "first comment", parentCommentId = (Guid?)null });
        var comment = await ReadAsync<CommentResponse>(commentResponse);
        var replyResponse = await author.PostAsJsonAsync(
            $"/api/posts/{post.Id}/comments",
            new { content = "reply", parentCommentId = comment.Id });
        var reply = await ReadAsync<CommentResponse>(replyResponse);
        var nestedReply = await reader.PostAsJsonAsync(
            $"/api/posts/{post.Id}/comments",
            new { content = "nested", parentCommentId = reply.Id });
        var forbiddenEdit = await author.PutAsJsonAsync(
            $"/api/posts/comments/{comment.Id}",
            new { content = "not mine" });

        var liked = await ReadAsync<PostResponse>(await reader.PutAsJsonAsync(
            $"/api/posts/{post.Id}/reaction",
            new { type = "love" }));
        var changed = await ReadAsync<PostResponse>(await reader.PutAsJsonAsync(
            $"/api/posts/{post.Id}/reaction",
            new { type = "wow" }));
        var removed = await ReadAsync<PostResponse>(await reader.DeleteAsync(
            $"/api/posts/{post.Id}/reaction"));
        var comments = await ReadAsync<PagedResponse<CommentResponse>>(
            await reader.GetAsync($"/api/posts/{post.Id}/comments"));

        Assert.Equal(HttpStatusCode.Created, commentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, nestedReply.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenEdit.StatusCode);
        Assert.Equal("love", liked.ViewerReaction);
        Assert.Equal(1, liked.ReactionCounts["love"]);
        Assert.Equal("wow", changed.ViewerReaction);
        Assert.False(changed.ReactionCounts.ContainsKey("love"));
        Assert.Null(removed.ViewerReaction);
        Assert.Empty(removed.ReactionCounts);
        Assert.Equal(2, comments.Total);
    }

    [Fact]
    public async Task Projections_are_idempotent_and_ignore_older_relationship_events()
    {
        var userEvent = new UserRegisteredIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), "projected_user", DateTimeOffset.UtcNow);
        Assert.True(await ProjectAsync(userEvent));
        Assert.False(await ProjectAsync(userEvent));

        var otherUserId = Guid.NewGuid();
        await ProjectAsync(new UserRegisteredIntegrationEvent(
            Guid.NewGuid(), otherUserId, "other_user", DateTimeOffset.UtcNow));
        var acceptedAt = DateTimeOffset.UtcNow;
        var removedAt = acceptedAt.AddSeconds(2);
        await ProjectAsync(new FriendshipRemovedIntegrationEvent(
            Guid.NewGuid(), Min(userEvent.UserId, otherUserId), Max(userEvent.UserId, otherUserId), removedAt));
        await ProjectAsync(new FriendRequestAcceptedIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Min(userEvent.UserId, otherUserId), Max(userEvent.UserId, otherUserId), acceptedAt));

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PostsDbContext>();
        Assert.Equal(1, await dbContext.KnownUsers.CountAsync(user => user.UserId == userEvent.UserId));
        Assert.Equal(1, await dbContext.InboxMessages.CountAsync(message => message.EventId == userEvent.EventId));
        var edge = await dbContext.FriendEdges.AsNoTracking().SingleAsync(item =>
            item.UserId1 == Min(userEvent.UserId, otherUserId) &&
            item.UserId2 == Max(userEvent.UserId, otherUserId));
        Assert.False(edge.IsActive);
        Assert.InRange(
            edge.LastChangedAtUtc,
            removedAt.AddMilliseconds(-1),
            removedAt.AddMilliseconds(1));
    }

    [Fact]
    public async Task Media_projection_is_idempotent_and_deleted_media_is_rejected()
    {
        var owner = (await CreateKnownUsersAsync(1))[0];
        var ready = ReadyMedia(owner);
        Assert.True(await ProjectAsync(ready));
        Assert.False(await ProjectAsync(ready));
        var deleted = new MediaDeletedIntegrationEvent(
            Guid.NewGuid(), ready.MediaId, owner, ready.OccurredAtUtc.AddSeconds(1));
        await ProjectAsync(deleted);

        using var client = CreateAuthenticatedClient(owner);
        var response = await client.PostAsJsonAsync("/api/posts",
            new { content = "deleted media", privacy = "public", mediaIds = new[] { ready.MediaId } });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostsDbContext>();
        Assert.False((await db.KnownMedia.SingleAsync(x => x.MediaId == ready.MediaId)).IsReady);
        Assert.Equal(2, await db.InboxMessages.CountAsync(x =>
            x.EventId == ready.EventId || x.EventId == deleted.EventId));
    }

    [Fact]
    public async Task Attachments_validate_owner_duplicates_maximum_and_allow_image_only_post()
    {
        var users = await CreateKnownUsersAsync(2);
        var own = ReadyMedia(users[0]);
        var foreign = ReadyMedia(users[1]);
        await ProjectAsync(own);
        await ProjectAsync(foreign);
        using var author = CreateAuthenticatedClient(users[0]);

        var imageOnlyResponse = await author.PostAsJsonAsync("/api/posts",
            new { content = "", privacy = "public", mediaIds = new[] { own.MediaId } });
        var imageOnly = await ReadAsync<PostResponse>(imageOnlyResponse);
        Assert.Equal(new[] { own.MediaId }, imageOnly.MediaIds);

        Assert.Equal(HttpStatusCode.Forbidden, (await author.PostAsJsonAsync("/api/posts",
            new { content = "foreign", privacy = "public", mediaIds = new[] { foreign.MediaId } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await author.PostAsJsonAsync("/api/posts",
            new { content = "duplicate", privacy = "public", mediaIds = new[] { own.MediaId, own.MediaId } })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await author.PostAsJsonAsync("/api/posts",
            new { content = "pending", privacy = "public", mediaIds = new[] { Guid.NewGuid() } })).StatusCode);

        var eleven = new List<Guid>();
        for (var index = 0; index < 11; index++)
        {
            var media = ReadyMedia(users[0]);
            await ProjectAsync(media);
            eleven.Add(media.MediaId);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await author.PostAsJsonAsync("/api/posts",
            new { content = "too many", privacy = "public", mediaIds = eleven })).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostsDbContext>();
        Assert.True(await db.PostMedia.AnyAsync(x => x.PostId == imageOnly.Id && x.MediaId == own.MediaId));
        var attachedEvents = await db.OutboxMessages
            .Where(x => x.Type == PostMediaAttachedIntegrationEvent.EventType).ToListAsync();
        Assert.Contains(attachedEvents, x => x.Payload.Contains(own.MediaId.ToString()));
    }

    [Fact]
    public async Task Post_privacy_and_blocks_gate_signed_media_access()
    {
        var users = await CreateKnownUsersAsync(3);
        var authorId = users[0]; var friendId = users[1]; var strangerId = users[2];
        await ProjectAsync(new FriendRequestAcceptedIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Min(authorId, friendId), Max(authorId, friendId), DateTimeOffset.UtcNow));
        var media = ReadyMedia(authorId);
        await ProjectAsync(media);
        using var author = CreateAuthenticatedClient(authorId);
        using var friend = CreateAuthenticatedClient(friendId);
        using var stranger = CreateAuthenticatedClient(strangerId);

        var friendsPost = await CreatePostAsync(author, "friends media", "friends", [media.MediaId]);
        Assert.Equal(HttpStatusCode.OK,
            (await friend.GetAsync($"/api/posts/{friendsPost.Id}/media/{media.MediaId}/access")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.GetAsync($"/api/posts/{friendsPost.Id}/media/{media.MediaId}/access")).StatusCode);

        await ProjectAsync(new UserBlockedIntegrationEvent(
            Guid.NewGuid(), authorId, friendId, DateTimeOffset.UtcNow.AddSeconds(1)));
        Assert.Equal(HttpStatusCode.NotFound,
            (await friend.GetAsync($"/api/posts/{friendsPost.Id}/media/{media.MediaId}/access")).StatusCode);

        var onlyMe = await CreatePostAsync(author, "private", "onlyMe", [media.MediaId]);
        Assert.Equal(HttpStatusCode.OK,
            (await author.GetAsync($"/api/posts/{onlyMe.Id}/media/{media.MediaId}/access")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.GetAsync($"/api/posts/{onlyMe.Id}/media/{media.MediaId}/access")).StatusCode);

        var publicPost = await CreatePostAsync(author, "public", "public", [media.MediaId]);
        var publicAccess = await stranger.GetAsync($"/api/posts/{publicPost.Id}/media/{media.MediaId}/access");
        var access = await ReadAsync<MediaAccessResponse>(publicAccess);
        Assert.Contains("signed=1", access.Url);
    }

    [Fact]
    public async Task Updating_attachments_emits_attach_and_detach_events_atomically()
    {
        var owner = (await CreateKnownUsersAsync(1))[0];
        var first = ReadyMedia(owner); var second = ReadyMedia(owner);
        await ProjectAsync(first); await ProjectAsync(second);
        using var client = CreateAuthenticatedClient(owner);
        var post = await CreatePostAsync(client, "with first", "public", [first.MediaId]);

        var updated = await ReadAsync<PostResponse>(await client.PutAsJsonAsync($"/api/posts/{post.Id}",
            new { content = "with second", privacy = "public", mediaIds = new[] { second.MediaId } }));
        Assert.Equal(new[] { second.MediaId }, updated.MediaIds);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostsDbContext>();
        Assert.False(await db.PostMedia.AnyAsync(x => x.PostId == post.Id && x.MediaId == first.MediaId));
        Assert.True(await db.PostMedia.AnyAsync(x => x.PostId == post.Id && x.MediaId == second.MediaId));
        var events = await db.OutboxMessages.Where(x =>
            x.Type == PostMediaAttachedIntegrationEvent.EventType ||
            x.Type == PostMediaDetachedIntegrationEvent.EventType).ToListAsync();
        Assert.Contains(events, x => x.Type == PostMediaDetachedIntegrationEvent.EventType &&
            x.Payload.Contains(first.MediaId.ToString()));
        Assert.Contains(events, x => x.Type == PostMediaAttachedIntegrationEvent.EventType &&
            x.Payload.Contains(second.MediaId.ToString()));
    }

    private async Task<Guid[]> CreateKnownUsersAsync(int count)
    {
        var userIds = new Guid[count];
        for (var index = 0; index < count; index++)
        {
            var integrationEvent = new UserRegisteredIntegrationEvent(
                Guid.NewGuid(),
                Guid.NewGuid(),
                $"user_{Guid.NewGuid():N}"[..21],
                DateTimeOffset.UtcNow);
            await ProjectAsync(integrationEvent);
            userIds[index] = integrationEvent.UserId;
        }

        return userIds;
    }

    private async Task<PostResponse> CreatePostAsync(
        HttpClient client, string content, string privacy, IReadOnlyList<Guid>? mediaIds = null)
    {
        var response = await client.PostAsJsonAsync("/api/posts", new { content, privacy, mediaIds });
        return await ReadAsync<PostResponse>(response);
    }

    private async Task<bool> ProjectAsync(object integrationEvent)
    {
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<EventProjectionStore>();
        return integrationEvent switch
        {
            UserRegisteredIntegrationEvent value => await store.ProjectAsync(value, default),
            FriendRequestAcceptedIntegrationEvent value => await store.ProjectAsync(value, default),
            FriendshipRemovedIntegrationEvent value => await store.ProjectAsync(value, default),
            UserBlockedIntegrationEvent value => await store.ProjectAsync(value, default),
            MediaReadyIntegrationEvent value => await store.ProjectAsync(value, default),
            MediaDeletedIntegrationEvent value => await store.ProjectAsync(value, default),
            _ => throw new ArgumentOutOfRangeException(nameof(integrationEvent))
        };
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

    private static Guid Min(Guid first, Guid second) => first.CompareTo(second) < 0 ? first : second;

    private static Guid Max(Guid first, Guid second) => first.CompareTo(second) > 0 ? first : second;

    private static MediaReadyIntegrationEvent ReadyMedia(Guid ownerId) => new(
        Guid.NewGuid(), Guid.NewGuid(), ownerId, "image", "image/png", 11, DateTimeOffset.UtcNow);
}
