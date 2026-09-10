using Fookbase.Api.Modules.Posts.DTOs.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Media.Data;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Posts.Data;
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
    public async Task Users_can_report_posts_and_user_profiles_once()
    {
        var users = await CreateUserIdsAsync(3);
        var authorUserId = users[0];
        var reporterUserId = users[1];
        var reportedUserId = users[2];
        using var author = CreateAuthenticatedClient(authorUserId);
        using var reporter = CreateAuthenticatedClient(reporterUserId);
        using var anonymous = factory.CreateClient();
        var post = await CreatePostAsync(author, "reportable", "public");

        var postReport = await reporter.PostAsJsonAsync(
            $"/api/reports/posts/{post.Id}",
            new { reason = "harassment", details = "Repeated abusive language." });
        var duplicatePostReport = await reporter.PostAsJsonAsync(
            $"/api/reports/posts/{post.Id}",
            new { reason = "spam" });
        var ownPostReport = await author.PostAsJsonAsync(
            $"/api/reports/posts/{post.Id}",
            new { reason = "spam" });
        var userReport = await reporter.PostAsJsonAsync(
            $"/api/reports/users/{reportedUserId}",
            new { reason = "scam" });
        var ownUserReport = await reporter.PostAsJsonAsync(
            $"/api/reports/users/{reporterUserId}",
            new { reason = "spam" });
        var anonymousReport = await anonymous.PostAsJsonAsync(
            $"/api/reports/users/{reportedUserId}",
            new { reason = "spam" });

        Assert.Equal(HttpStatusCode.Created, postReport.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicatePostReport.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, ownPostReport.StatusCode);
        Assert.Equal(HttpStatusCode.Created, userReport.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, ownUserReport.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousReport.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PostsDbContext>();
        var reports = await dbContext.ContentReports
            .Where(report => report.ReporterUserId == reporterUserId)
            .ToListAsync();
        Assert.Contains(reports, report =>
            report.TargetType == ReportTargetType.Post && report.TargetId == post.Id &&
            report.Reason == ReportReason.Harassment && report.Details == "Repeated abusive language.");
        Assert.Contains(reports, report =>
            report.TargetType == ReportTargetType.User && report.TargetId == reportedUserId &&
            report.Reason == ReportReason.Scam);
    }

    [Fact]
    public async Task Administrators_can_review_reports_and_remove_reported_posts()
    {
        var users = await CreateUserIdsAsync(3);
        using var author = CreateAuthenticatedClient(users[0]);
        using var reporter = CreateAuthenticatedClient(users[1]);
        using var administrator = CreateAuthenticatedClient(users[2], ["Admin"]);
        var post = await CreatePostAsync(author, "reportable content", "public");
        var reportResponse = await reporter.PostAsJsonAsync(
            $"/api/reports/posts/{post.Id}",
            new { reason = "spam", details = "Unwanted promotion." });
        var report = await ReadAsync<ContentReportResponse>(reportResponse);

        var forbidden = await author.GetAsync("/api/admin/reports");
        var reportsResponse = await administrator.GetAsync("/api/admin/reports?status=pending");
        var reports = await ReadAsync<PagedResponse<ModerationReportResponse>>(reportsResponse);
        var reviewResponse = await administrator.PatchAsJsonAsync(
            $"/api/admin/reports/{report.Id}/status",
            new { status = "resolved" });
        var reviewed = await ReadAsync<ModerationReportResponse>(reviewResponse);
        var deleteResponse = await administrator.DeleteAsync($"/api/admin/posts/{post.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Contains(reports.Items, item => item.Id == report.Id && item.ReporterUserId == users[1]);
        Assert.Equal("resolved", reviewed.Status);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PostsDbContext>();
        Assert.NotNull((await dbContext.Posts.SingleAsync(item => item.Id == post.Id)).DeletedAtUtc);
    }

    [Fact]
    public async Task Author_can_create_update_and_soft_delete_post()
    {
        var users = await CreateUserIdsAsync(2);
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
    }

    [Fact]
    public async Task Privacy_feed_and_block_relationships_control_visibility()
    {
        var users = await CreateUserIdsAsync(3);
        var authorId = users[0];
        var friendId = users[1];
        var strangerId = users[2];
        await CreateFriendshipAsync(authorId, friendId);
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
        var friendSearch = await ReadAsync<PagedResponse<PostResponse>>(
            await friend.GetAsync("/api/posts/search?query=friends"));
        var strangerSearch = await ReadAsync<PagedResponse<PostResponse>>(
            await stranger.GetAsync("/api/posts/search?query=friends"));

        Assert.Single(anonymousPosts.Items);
        Assert.Equal(publicPost.Id, anonymousPosts.Items[0].Id);
        Assert.Contains(friendFeed.Items, item => item.Id == publicPost.Id);
        Assert.Contains(friendFeed.Items, item => item.Id == friendsPost.Id);
        Assert.DoesNotContain(friendFeed.Items, item => item.Id == privatePost.Id);
        Assert.Contains(strangerFeed.Items, item => item.Id == publicPost.Id);
        Assert.DoesNotContain(strangerFeed.Items, item => item.Id == friendsPost.Id);
        Assert.Contains(friendSearch.Items, item => item.Id == friendsPost.Id);
        Assert.DoesNotContain(strangerSearch.Items, item => item.Id == friendsPost.Id);
        Assert.DoesNotContain(strangerFeed.Items, item => item.Id == privatePost.Id);

        await BlockAsync(authorId, friendId);
        var blockedFeed = await ReadAsync<PagedResponse<PostResponse>>(
            await friend.GetAsync("/api/posts/feed"));
        var blockedDirect = await friend.GetAsync($"/api/posts/{friendsPost.Id}");

        Assert.DoesNotContain(blockedFeed.Items, item => item.AuthorUserId == authorId);
        Assert.Equal(HttpStatusCode.NotFound, blockedDirect.StatusCode);
    }

    [Fact]
    public async Task Comments_and_reactions_enforce_access_and_return_summaries()
    {
        var users = await CreateUserIdsAsync(2);
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
    public async Task Relationship_access_is_read_directly_from_Friends()
    {
        var firstUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await CreateFriendshipAsync(firstUserId, otherUserId);

        using var scope = factory.Services.CreateScope();
        var friends = scope.ServiceProvider.GetRequiredService<FriendsService>();
        var friendship = await friends.GetAccessSnapshotAsync(firstUserId);
        Assert.Contains(otherUserId, friendship.FriendUserIds);

        await BlockAsync(firstUserId, otherUserId);
        var blocked = await friends.GetAccessSnapshotAsync(firstUserId);
        Assert.Contains(otherUserId, blocked.BlockedUserIds);
    }

    [Fact]
    public async Task Deleted_media_is_rejected()
    {
        var owner = (await CreateUserIdsAsync(1))[0];
        var mediaId = await CreateReadyMediaAsync(owner);
        await DeleteMediaAsync(mediaId);

        using var client = CreateAuthenticatedClient(owner);
        var response = await client.PostAsJsonAsync("/api/posts",
            new { content = "deleted media", privacy = "public", mediaIds = new[] { mediaId } });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        Assert.Equal(MediaStatus.Deleted,
            (await db.MediaAssets.SingleAsync(x => x.Id == mediaId)).Status);
    }

    [Fact]
    public async Task Attachments_validate_owner_duplicates_maximum_and_allow_image_only_post()
    {
        var users = await CreateUserIdsAsync(2);
        var own = await CreateReadyMediaAsync(users[0]);
        var foreign = await CreateReadyMediaAsync(users[1]);
        using var author = CreateAuthenticatedClient(users[0]);

        var imageOnlyResponse = await author.PostAsJsonAsync("/api/posts",
            new { content = "", privacy = "public", mediaIds = new[] { own } });
        var imageOnly = await ReadAsync<PostResponse>(imageOnlyResponse);
        Assert.Equal(new[] { own }, imageOnly.MediaIds);

        Assert.Equal(HttpStatusCode.Forbidden, (await author.PostAsJsonAsync("/api/posts",
            new { content = "foreign", privacy = "public", mediaIds = new[] { foreign } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await author.PostAsJsonAsync("/api/posts",
            new { content = "duplicate", privacy = "public", mediaIds = new[] { own, own } })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await author.PostAsJsonAsync("/api/posts",
            new { content = "pending", privacy = "public", mediaIds = new[] { Guid.NewGuid() } })).StatusCode);

        var eleven = new List<Guid>();
        for (var index = 0; index < 11; index++)
        {
            eleven.Add(await CreateReadyMediaAsync(users[0]));
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await author.PostAsJsonAsync("/api/posts",
            new { content = "too many", privacy = "public", mediaIds = eleven })).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostsDbContext>();
        Assert.True(await db.PostMedia.AnyAsync(x => x.PostId == imageOnly.Id && x.MediaId == own));
        var mediaDb = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        Assert.True(await mediaDb.MediaReferences.AnyAsync(x => x.PostId == imageOnly.Id && x.MediaId == own));
    }

    [Fact]
    public async Task Post_privacy_and_blocks_gate_signed_media_access()
    {
        var users = await CreateUserIdsAsync(3);
        var authorId = users[0]; var friendId = users[1]; var strangerId = users[2];
        await CreateFriendshipAsync(authorId, friendId);
        var mediaId = await CreateReadyMediaAsync(authorId);
        using var author = CreateAuthenticatedClient(authorId);
        using var friend = CreateAuthenticatedClient(friendId);
        using var stranger = CreateAuthenticatedClient(strangerId);

        var friendsPost = await CreatePostAsync(author, "friends media", "friends", [mediaId]);
        Assert.Equal(HttpStatusCode.OK,
            (await friend.GetAsync($"/api/posts/{friendsPost.Id}/media/{mediaId}/access")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.GetAsync($"/api/posts/{friendsPost.Id}/media/{mediaId}/access")).StatusCode);

        await BlockAsync(authorId, friendId);
        Assert.Equal(HttpStatusCode.NotFound,
            (await friend.GetAsync($"/api/posts/{friendsPost.Id}/media/{mediaId}/access")).StatusCode);

        var onlyMe = await CreatePostAsync(author, "private", "onlyMe", [mediaId]);
        Assert.Equal(HttpStatusCode.OK,
            (await author.GetAsync($"/api/posts/{onlyMe.Id}/media/{mediaId}/access")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.GetAsync($"/api/posts/{onlyMe.Id}/media/{mediaId}/access")).StatusCode);

        var publicPost = await CreatePostAsync(author, "public", "public", [mediaId]);
        var publicAccess = await stranger.GetAsync($"/api/posts/{publicPost.Id}/media/{mediaId}/access");
        var access = await ReadAsync<MediaAccessResponse>(publicAccess);
        Assert.Contains("signed=1", access.Url);
        Assert.Equal("image", access.MediaType);
        Assert.Equal("image/png", access.ContentType);
    }

    [Fact]
    public async Task Media_access_includes_video_metadata_for_playback()
    {
        var users = await CreateUserIdsAsync(2);
        var videoId = await CreateReadyMediaAsync(users[0], MediaType.Video);
        using var author = CreateAuthenticatedClient(users[0]);
        using var viewer = CreateAuthenticatedClient(users[1]);

        var post = await CreatePostAsync(author, "video post", "public", [videoId]);
        var access = await ReadAsync<MediaAccessResponse>(
            await viewer.GetAsync($"/api/posts/{post.Id}/media/{videoId}/access"));

        Assert.Equal("video", access.MediaType);
        Assert.Equal("video/mp4", access.ContentType);
    }

    [Fact]
    public async Task Updating_attachments_synchronizes_media_references()
    {
        var owner = (await CreateUserIdsAsync(1))[0];
        var first = await CreateReadyMediaAsync(owner); var second = await CreateReadyMediaAsync(owner);
        using var client = CreateAuthenticatedClient(owner);
        var post = await CreatePostAsync(client, "with first", "public", [first]);

        var updated = await ReadAsync<PostResponse>(await client.PutAsJsonAsync($"/api/posts/{post.Id}",
            new { content = "with second", privacy = "public", mediaIds = new[] { second } }));
        Assert.Equal(new[] { second }, updated.MediaIds);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostsDbContext>();
        Assert.False(await db.PostMedia.AnyAsync(x => x.PostId == post.Id && x.MediaId == first));
        Assert.True(await db.PostMedia.AnyAsync(x => x.PostId == post.Id && x.MediaId == second));
        var mediaDb = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        Assert.False(await mediaDb.MediaReferences.AnyAsync(x => x.PostId == post.Id && x.MediaId == first));
        Assert.True(await mediaDb.MediaReferences.AnyAsync(x => x.PostId == post.Id && x.MediaId == second));
    }

    private static Task<Guid[]> CreateUserIdsAsync(int count) =>
        Task.FromResult(Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray());

    private async Task<PostResponse> CreatePostAsync(
        HttpClient client, string content, string privacy, IReadOnlyList<Guid>? mediaIds = null)
    {
        var response = await client.PostAsJsonAsync("/api/posts", new { content, privacy, mediaIds });
        return await ReadAsync<PostResponse>(response);
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

    private async Task<Guid> CreateReadyMediaAsync(Guid ownerUserId, MediaType mediaType = MediaType.Image)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var now = DateTimeOffset.UtcNow;
        var mediaId = Guid.NewGuid();
        var asset = MediaAsset.CreatePending(
            mediaId,
            ownerUserId,
            mediaType,
            $"{ownerUserId:N}/{mediaId:N}{(mediaType == MediaType.Video ? ".mp4" : ".png")}",
            mediaType == MediaType.Video ? "video.mp4" : "photo.png",
            mediaType == MediaType.Video ? "video/mp4" : "image/png",
            11,
            now,
            now.AddMinutes(5));
        asset.MarkReady(11, now);
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync();
        return mediaId;
    }

    private async Task DeleteMediaAsync(Guid mediaId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var asset = await db.MediaAssets.SingleAsync(item => item.Id == mediaId);
        asset.Delete(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
    }

    private HttpClient CreateAuthenticatedClient(Guid userId, IReadOnlyCollection<string>? roles = null)
    {
        using var scope = factory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange((roles ?? []).Select(role => new Claim(ClaimTypes.Role, role)));
        var token = new JwtSecurityToken(
            configuration["Jwt:Issuer"],
            configuration["Jwt:Audience"],
            claims,
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
