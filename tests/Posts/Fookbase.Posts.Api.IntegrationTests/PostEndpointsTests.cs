using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Fookbase.Contracts.Friends;
using Fookbase.Contracts.Identity;
using Fookbase.Contracts.Posts;
using Fookbase.Posts.Application.Posts;
using Fookbase.Posts.Infrastructure.Persistence;
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

    private async Task<PostResponse> CreatePostAsync(HttpClient client, string content, string privacy)
    {
        var response = await client.PostAsJsonAsync("/api/posts", new { content, privacy });
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
}
