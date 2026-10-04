using Fookbase.Api.Modules.Posts.DTOs.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Feed.DTOs.Responses;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Notifications.DTOs.Responses;
using Fookbase.Api.Modules.Notifications.Entities;
using Fookbase.Api.Modules.Users.Entities;
using Fookbase.Api.Modules.Admin.Entities;
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
    public async Task Profile_posts_keep_the_authenticated_user_as_author_and_container()
    {
        var authorUserId = (await CreateUserIdsAsync(1))[0];
        using var author = CreateAuthenticatedClient(authorUserId);

        var response = await CreatePostAsync(author, "profile author", "public");

        Assert.Equal(authorUserId, response.AuthorUserId);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var persisted = await db.Posts.SingleAsync(item => item.Id == response.Id);
        Assert.Equal(authorUserId, persisted.AuthorUserId);
        Assert.Equal(PostContainerType.PROFILE, persisted.ContainerType);
        Assert.Equal(authorUserId, persisted.ContainerId);
    }

    [Fact]
    public async Task Post_responses_include_the_user_display_identity_and_avatar()
    {
        var authorUserId = (await CreateUserIdsAsync(1))[0];
        const string username = "post_author";
        await CreateProfileAsync(authorUserId, username);
        var avatarMediaId = await CreateReadyMediaAsync(authorUserId);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            var profile = await db.UserProfiles.SingleAsync(item => item.UserId == authorUserId);
            profile.Update("Post Author", null, null, null, avatarMediaId, null, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        using var author = CreateAuthenticatedClient(authorUserId);
        var created = await CreatePostAsync(author, "post author identity", "public");
        var loaded = await ReadAsync<PostResponse>(await author.GetAsync($"/api/posts/{created.Id}"));

        var displayAuthor = Assert.IsType<PostDisplayIdentityResponse>(loaded.DisplayAuthor);
        Assert.Equal("user", displayAuthor.Type);
        Assert.Equal(authorUserId, displayAuthor.Id);
        Assert.Equal(username, displayAuthor.Username);
        Assert.Equal("Post Author", displayAuthor.Name);
        Assert.Equal($"/api/users/{authorUserId}/avatar", displayAuthor.AvatarUrl);
    }

    [Fact]
    public async Task Post_responses_fall_back_to_the_account_username_without_a_profile()
    {
        var authorUserId = Guid.NewGuid();
        const string username = "post_without_profile";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            db.Users.Add(new User(
                authorUserId,
                "post-without-profile@example.com",
                username,
                DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        using var author = CreateAuthenticatedClient(authorUserId);
        var created = await CreatePostAsync(author, "post without profile", "public");

        var displayAuthor = Assert.IsType<PostDisplayIdentityResponse>(created.DisplayAuthor);
        Assert.Equal(username, displayAuthor.Username);
        Assert.Equal(username, displayAuthor.Name);
        Assert.Null(displayAuthor.AvatarUrl);
    }

    [Fact]
    public async Task Text_posts_persist_selected_background_and_privacy()
    {
        var authorUserId = (await CreateUserIdsAsync(1))[0];
        using var author = CreateAuthenticatedClient(authorUserId);

        var response = await author.PostAsJsonAsync("/api/posts", new
        {
            content = "Bài viết có nền",
            privacy = "friends",
            textBackground = "sunset",
        });
        var created = await ReadAsync<PostResponse>(response);

        Assert.Equal("friends", created.Privacy);
        Assert.Equal("sunset", created.TextBackground);
        var feed = await ReadAsync<FeedPageResponse>(await author.GetAsync("/api/feed?limit=20"));
        Assert.Equal("sunset", Assert.Single(feed.Items, item => item.Id == created.Id).TextBackground);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Equal("sunset", (await db.Posts.SingleAsync(post => post.Id == created.Id)).TextBackground);

        var invalid = await author.PostAsJsonAsync("/api/posts", new
        {
            content = "Nền không hợp lệ",
            privacy = "public",
            textBackground = "custom-css",
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
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
        var missingUserReport = await reporter.PostAsJsonAsync(
            $"/api/reports/users/{Guid.NewGuid()}",
            new { reason = "spam" });

        Assert.Equal(HttpStatusCode.Created, postReport.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicatePostReport.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, ownPostReport.StatusCode);
        Assert.Equal(HttpStatusCode.Created, userReport.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, ownUserReport.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousReport.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingUserReport.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var reports = await dbContext.ContentReports
            .Where(report => report.ReporterUserId == reporterUserId)
            .ToListAsync();
        Assert.Contains(reports, report =>
            report.TargetType == ReportTargetType.POST && report.TargetId == post.Id &&
            report.Reason == ReportReason.HARASSMENT && report.Details == "Repeated abusive language.");
        Assert.Contains(reports, report =>
            report.TargetType == ReportTargetType.USER && report.TargetId == reportedUserId &&
            report.Reason == ReportReason.SCAM);
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
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.NotNull((await dbContext.Posts.SingleAsync(item => item.Id == post.Id)).DeletedAtUtc);
    }

    [Fact]
    public async Task Admin_removing_a_reported_post_records_action_and_marks_report_reviewed()
    {
        var users = await CreateUserIdsAsync(3);
        using var author = CreateAuthenticatedClient(users[0]);
        using var reporter = CreateAuthenticatedClient(users[1]);
        using var administrator = CreateAuthenticatedClient(users[2], ["Admin"]);
        var post = await CreatePostAsync(author, "moderation target", "public");
        var report = await ReadAsync<ContentReportResponse>(await reporter.PostAsJsonAsync(
            $"/api/reports/posts/{post.Id}", new { reason = "harassment" }));

        var removed = await administrator.PostAsJsonAsync($"/api/admin/reports/{report.Id}/remove-content", new { reason = "Policy violation" });

        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.NotNull((await db.Posts.SingleAsync(item => item.Id == post.Id)).DeletedAtUtc);
        Assert.Equal(ContentReportStatus.REVIEWED, (await db.ContentReports.SingleAsync(item => item.Id == report.Id)).Status);
        Assert.Contains(await db.ModerationActions.ToListAsync(), action => action.ReportId == report.Id && action.ActionType == ModerationActionType.REMOVE_POST);
    }

    [Fact]
    public async Task Dismissing_a_report_is_idempotent_and_keeps_a_single_audit_action()
    {
        var users = await CreateUserIdsAsync(3);
        using var author = CreateAuthenticatedClient(users[0]);
        using var reporter = CreateAuthenticatedClient(users[1]);
        using var administrator = CreateAuthenticatedClient(users[2], ["Admin"]);
        var post = await CreatePostAsync(author, "dismiss target", "public");
        var report = await ReadAsync<ContentReportResponse>(await reporter.PostAsJsonAsync(
            $"/api/reports/posts/{post.Id}", new { reason = "spam" }));

        Assert.Equal(HttpStatusCode.OK, (await administrator.PostAsJsonAsync($"/api/admin/reports/{report.Id}/dismiss", new { reason = "No violation" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await administrator.PostAsJsonAsync($"/api/admin/reports/{report.Id}/dismiss", new { reason = "No violation" })).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Equal(ContentReportStatus.DISMISSED, (await db.ContentReports.SingleAsync(item => item.Id == report.Id)).Status);
        Assert.Equal(1, await db.ModerationActions.CountAsync(action => action.ReportId == report.Id && action.ActionType == ModerationActionType.DISMISS_REPORT));
    }

    [Fact]
    public async Task Suspension_blocks_mutations_until_an_admin_unsuspends_the_account()
    {
        var users = await CreateUserIdsAsync(2);
        using var member = CreateAuthenticatedClient(users[0]);
        using var administrator = CreateAuthenticatedClient(users[1], ["Admin"]);

        Assert.Equal(HttpStatusCode.OK, (await administrator.PostAsJsonAsync($"/api/admin/users/{users[0]}/suspend", new { reason = "Cooling off", durationHours = 24 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync("/api/posts", new { content = "blocked", privacy = "public" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await administrator.PostAsJsonAsync($"/api/admin/users/{users[0]}/unsuspend", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await member.PostAsJsonAsync("/api/posts", new { content = "allowed again", privacy = "public" })).StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_suspend_their_own_account()
    {
        var administratorUserId = (await CreateUserIdsAsync(1))[0];
        using var administrator = CreateAuthenticatedClient(administratorUserId, ["Admin"]);

        var response = await administrator.PostAsJsonAsync($"/api/admin/users/{administratorUserId}/suspend", new { reason = "unsafe", durationHours = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.NotNull((await dbContext.Posts.AsNoTracking().SingleAsync(post => post.Id == created.Id)).DeletedAtUtc);
    }

    [Fact]
    public async Task Author_can_pin_one_profile_post_and_it_is_listed_first()
    {
        var users = await CreateUserIdsAsync(2);
        using var author = CreateAuthenticatedClient(users[0]);
        using var other = CreateAuthenticatedClient(users[1]);
        var first = await CreatePostAsync(author, "first profile post", "public");
        var second = await CreatePostAsync(author, "second profile post", "public");

        var forbidden = await other.PutAsync($"/api/posts/{first.Id}/pin", null);
        var pinned = await ReadAsync<PostResponse>(await author.PutAsync($"/api/posts/{first.Id}/pin", null));
        var pinnedSecond = await ReadAsync<PostResponse>(await author.PutAsync($"/api/posts/{second.Id}/pin", null));
        var firstAfterSecondPin = await ReadAsync<PostResponse>(await author.GetAsync($"/api/posts/{first.Id}"));
        var profilePosts = await ReadAsync<PagedResponse<PostResponse>>(
            await author.GetAsync($"/api/posts/users/{users[0]}"));
        var unpinned = await ReadAsync<PostResponse>(await author.DeleteAsync($"/api/posts/{second.Id}/pin"));

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.True(pinned.IsPinned);
        Assert.True(pinnedSecond.IsPinned);
        Assert.False(firstAfterSecondPin.IsPinned);
        Assert.Equal(second.Id, profilePosts.Items[0].Id);
        Assert.True(profilePosts.Items[0].IsPinned);
        Assert.False(unpinned.IsPinned);
    }

    [Fact]
    public async Task Saved_posts_are_idempotent_private_and_rechecked_when_access_changes()
    {
        var users = await CreateUserIdsAsync(2);
        using var author = CreateAuthenticatedClient(users[0]);
        using var viewer = CreateAuthenticatedClient(users[1]);
        var post = await CreatePostAsync(author, "friends only saved post", "friends");

        Assert.Equal(HttpStatusCode.NotFound, (await viewer.PostAsync($"/api/posts/{post.Id}/save", null)).StatusCode);
        await CreateFriendshipAsync(users[0], users[1]);

        Assert.Equal(HttpStatusCode.NoContent, (await viewer.PostAsync($"/api/posts/{post.Id}/save", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await viewer.PostAsync($"/api/posts/{post.Id}/save", null)).StatusCode);
        var savedResponse = await viewer.GetAsync("/api/posts/saved?limit=1");
        Assert.True(savedResponse.IsSuccessStatusCode, await savedResponse.Content.ReadAsStringAsync());
        var saved = await ReadAsync<SavedPostsPageResponse>(savedResponse);
        Assert.Single(saved.Items);
        Assert.Equal(post.Id, saved.Items[0].Id);
        Assert.True(saved.Items[0].ViewerHasSaved);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/posts/saved")).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            Assert.Equal(1, await db.PostSaves.CountAsync(save => save.UserId == users[1] && save.PostId == post.Id));
            var friendship = await db.Friendships.SingleAsync(item =>
                (item.UserId1 == users[0] && item.UserId2 == users[1]) ||
                (item.UserId1 == users[1] && item.UserId2 == users[0]));
            db.Friendships.Remove(friendship);
            await db.SaveChangesAsync();
        }

        var inaccessible = await ReadAsync<SavedPostsPageResponse>(await viewer.GetAsync("/api/posts/saved"));
        Assert.Empty(inaccessible.Items);
        Assert.Equal(HttpStatusCode.NoContent, (await viewer.DeleteAsync($"/api/posts/{post.Id}/save")).StatusCode);
    }

    [Fact]
    public async Task Shares_keep_a_single_original_and_mentions_hashtags_are_resolved_safely()
    {
        var users = await CreateUserIdsAsync(3);
        var authorUserId = users[0];
        var sharerUserId = users[1];
        var mentionedUserId = users[2];
        var mentionedUsername = "social_" + Guid.NewGuid().ToString("N")[..12];
        await CreateProfileAsync(mentionedUserId, mentionedUsername);
        using var author = CreateAuthenticatedClient(authorUserId);
        using var sharer = CreateAuthenticatedClient(sharerUserId);
        using var mentioned = CreateAuthenticatedClient(mentionedUserId);
        var post = await CreatePostAsync(author, $"Hello @{mentionedUsername} #FookbaseLight", "public");

        Assert.Single(post.Mentions!);
        Assert.Equal(mentionedUserId, post.Mentions![0].UserId);
        var shareResponse = await sharer.PostAsJsonAsync($"/api/posts/{post.Id}/shares", new
        {
            destinationType = "profile",
            destinationId = sharerUserId,
            caption = "Useful post"
        });
        var share = await ReadAsync<PostShareResponse>(shareResponse);
        Assert.Equal(post.Id, share.OriginalPost.Id);
        Assert.Equal("profile", share.DestinationType);
        var refreshedPost = await ReadAsync<PostResponse>(await author.GetAsync($"/api/posts/{post.Id}"));
        Assert.Equal(1, refreshedPost.ShareCount);
        Assert.Equal(HttpStatusCode.Forbidden, (await sharer.PostAsJsonAsync($"/api/posts/{post.Id}/shares", new
        {
            destinationType = "profile",
            destinationId = authorUserId
        })).StatusCode);

        var mentionNotifications = await ReadAsync<NotificationPageResponse>(await mentioned.GetAsync("/api/notifications"));
        Assert.Contains(mentionNotifications.Items, item => item.Type == "PostMention" && item.EntityId == post.Id);
        var hashtagPage = await ReadAsync<HashtagPostsPageResponse>(
            await factory.CreateClient().GetAsync("/api/hashtags/fookbaselight/posts?limit=1"));
        Assert.Contains(hashtagPage.Items, item => item.Id == post.Id);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Equal(1, await db.PostShares.CountAsync(item => item.OriginalPostId == post.Id));
        Assert.Equal(1, await db.PostHashtags.CountAsync(item => item.PostId == post.Id));
        Assert.Single(await db.Notifications.Where(item =>
            item.Type == NotificationType.POST_SHARED && item.EntityId == post.Id).ToListAsync());
    }

    [Fact]
    public async Task Profile_shares_are_feed_events_and_disappear_when_the_original_becomes_inaccessible()
    {
        var users = await CreateUserIdsAsync(3);
        var authorUserId = users[0];
        var sharerUserId = users[1];
        var viewerUserId = users[2];
        const string authorUsername = "shared_author";
        await CreateProfileAsync(authorUserId, authorUsername);
        var avatarMediaId = await CreateReadyMediaAsync(authorUserId);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            var profile = await db.UserProfiles.SingleAsync(item => item.UserId == authorUserId);
            profile.Update("Shared Author", null, null, null, avatarMediaId, null, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        await CreateFriendshipAsync(sharerUserId, viewerUserId);
        using var author = CreateAuthenticatedClient(authorUserId);
        using var sharer = CreateAuthenticatedClient(sharerUserId);
        using var viewer = CreateAuthenticatedClient(viewerUserId);
        var original = await CreatePostAsync(author, "shared feed original", "public");
        var share = await ReadAsync<PostShareResponse>(await sharer.PostAsJsonAsync(
            $"/api/posts/{original.Id}/shares",
            new { destinationType = "profile", destinationId = sharerUserId, caption = "Read this" }));

        var feed = await ReadAsync<FeedPageResponse>(await viewer.GetAsync("/api/feed?limit=50"));
        var feedShare = Assert.Single(feed.Items, item => item.Id == share.Id);
        Assert.Equal("share", feedShare.ContentType);
        Assert.NotNull(feedShare.Share);
        Assert.Equal(original.Id, feedShare.Share!.OriginalPostId);
        Assert.Equal(original.Id, feedShare.Share.OriginalPost.Id);
        Assert.Equal("Shared Author", feedShare.Share.OriginalAuthor.Name);
        Assert.Equal(authorUsername, feedShare.Share.OriginalAuthor.Username);
        Assert.Equal($"/api/users/{authorUserId}/avatar", feedShare.Share.OriginalAuthor.AvatarUrl);

        Assert.Equal(HttpStatusCode.OK, (await author.PutAsJsonAsync($"/api/posts/{original.Id}", new
        {
            content = "shared feed original", privacy = "onlyMe"
        })).StatusCode);
        var refreshed = await ReadAsync<FeedPageResponse>(await viewer.GetAsync("/api/feed?limit=50"));
        Assert.DoesNotContain(refreshed.Items, item => item.Id == share.Id);
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
        Assert.NotNull(comment.Author);
        Assert.Equal(users[1], comment.Author!.UserId);
        Assert.NotNull(reply.Author);
        Assert.Equal(users[0], reply.Author!.UserId);
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
    public async Task Reaction_list_returns_reactors_and_supports_filtering_by_type()
    {
        var users = await CreateUserIdsAsync(3);
        using var author = CreateAuthenticatedClient(users[0]);
        using var firstReactor = CreateAuthenticatedClient(users[1]);
        using var secondReactor = CreateAuthenticatedClient(users[2]);
        var post = await CreatePostAsync(author, "reaction list", "public");

        await firstReactor.PutAsJsonAsync($"/api/posts/{post.Id}/reaction", new { type = "love" });
        await secondReactor.PutAsJsonAsync($"/api/posts/{post.Id}/reaction", new { type = "haha" });
        var all = await ReadAsync<PagedResponse<PostReactionResponse>>(
            await author.GetAsync($"/api/posts/{post.Id}/reactions"));
        var loves = await ReadAsync<PagedResponse<PostReactionResponse>>(
            await author.GetAsync($"/api/posts/{post.Id}/reactions?type=love"));

        Assert.Equal(2, all.Total);
        Assert.Contains(all.Items, item => item.UserId == users[1] && item.Type == "love");
        Assert.Contains(all.Items, item => item.UserId == users[2] && item.Type == "haha");
        Assert.Equal(1, loves.Total);
        Assert.Equal(users[1], Assert.Single(loves.Items).UserId);
        Assert.Equal("love", Assert.Single(loves.Items).Type);
        Assert.Equal("none", Assert.Single(loves.Items).RelationshipStatus);

        await BlockAsync(users[0], users[2]);
        var afterBlock = await ReadAsync<PagedResponse<PostReactionResponse>>(
            await author.GetAsync($"/api/posts/{post.Id}/reactions"));
        Assert.Equal(1, afterBlock.Total);
        Assert.DoesNotContain(afterBlock.Items, item => item.UserId == users[2]);
    }

    [Fact]
    public async Task Friend_request_and_acceptance_create_general_notifications()
    {
        var users = await CreateUserIdsAsync(2);
        using var sender = CreateAuthenticatedClient(users[0]);
        using var receiver = CreateAuthenticatedClient(users[1]);

        var request = await ReadAsync<Fookbase.Api.Modules.Friends.DTOs.Responses.FriendRequestResponse>(
            await sender.PostAsync("/api/friends/requests/" + users[1], null));
        var acceptance = await receiver.PostAsync(
            "/api/friends/requests/" + request.Id + "/accept",
            null);
        var receiverNotifications = await ReadAsync<NotificationPageResponse>(
            await receiver.GetAsync("/api/notifications"));
        var senderNotifications = await ReadAsync<NotificationPageResponse>(
            await sender.GetAsync("/api/notifications"));

        Assert.Equal(HttpStatusCode.OK, acceptance.StatusCode);
        Assert.Contains(receiverNotifications.Items, item =>
            item.Type == "FriendRequestReceived" &&
            item.ActorUserId == users[0] &&
            item.EntityId == request.Id);
        Assert.Contains(senderNotifications.Items, item =>
            item.Type == "FriendRequestAccepted" &&
            item.ActorUserId == users[1] &&
            item.EntityId == request.Id);
    }

    [Fact]
    public async Task Post_reactions_are_deduplicated_and_own_actions_do_not_notify()
    {
        var users = await CreateUserIdsAsync(2);
        using var author = CreateAuthenticatedClient(users[0]);
        using var actor = CreateAuthenticatedClient(users[1]);
        var post = await CreatePostAsync(author, "reaction target", "public");

        Assert.Equal(HttpStatusCode.OK, (await author.PutAsJsonAsync(
            "/api/posts/" + post.Id + "/reaction",
            new { type = "like" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await actor.PutAsJsonAsync(
            "/api/posts/" + post.Id + "/reaction",
            new { type = "love" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await actor.PutAsJsonAsync(
            "/api/posts/" + post.Id + "/reaction",
            new { type = "wow" })).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var notifications = await db.Notifications.AsNoTracking()
            .Where(item =>
                item.Type == NotificationType.POST_REACTION &&
                item.EntityId == post.Id)
            .ToListAsync();

        var notification = Assert.Single(notifications);
        Assert.Equal(users[0], notification.RecipientUserId);
        Assert.Equal(users[1], notification.ActorUserId);
        Assert.DoesNotContain(await db.Notifications.AsNoTracking().ToListAsync(), item =>
            item.Type == NotificationType.POST_REACTION &&
            item.RecipientUserId == users[0] &&
            item.ActorUserId == users[0]);
    }

    [Fact]
    public async Task Comments_and_comment_reactions_create_general_notifications()
    {
        var users = await CreateUserIdsAsync(3);
        using var author = CreateAuthenticatedClient(users[0]);
        using var commenter = CreateAuthenticatedClient(users[1]);
        using var reactor = CreateAuthenticatedClient(users[2]);
        var post = await CreatePostAsync(author, "comment target", "public");
        var comment = await ReadAsync<CommentResponse>(await commenter.PostAsJsonAsync(
            "/api/posts/" + post.Id + "/comments",
            new { content = "a comment", parentCommentId = (Guid?)null }));

        var reaction = await reactor.PutAsJsonAsync(
            "/api/posts/comments/" + comment.Id + "/reaction",
            new { type = "love" });

        Assert.Equal(HttpStatusCode.OK, reaction.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Contains(await db.Notifications.AsNoTracking().ToListAsync(), item =>
            item.Type == NotificationType.POST_COMMENT &&
            item.RecipientUserId == users[0] &&
            item.ActorUserId == users[1] &&
            item.EntityType == NotificationEntityType.POST &&
            item.EntityId == post.Id);
        Assert.Contains(await db.Notifications.AsNoTracking().ToListAsync(), item =>
            item.Type == NotificationType.COMMENT_REACTION &&
            item.RecipientUserId == users[1] &&
            item.ActorUserId == users[2] &&
            item.EntityType == NotificationEntityType.COMMENT &&
            item.EntityId == comment.Id);
    }

    [Fact]
    public async Task Comment_reaction_notification_includes_its_parent_post_id()
    {
        var users = await CreateUserIdsAsync(3);
        using var author = CreateAuthenticatedClient(users[0]);
        using var commenter = CreateAuthenticatedClient(users[1]);
        using var reactor = CreateAuthenticatedClient(users[2]);
        var post = await CreatePostAsync(author, "notification comment target", "public");
        var comment = await ReadAsync<CommentResponse>(await commenter.PostAsJsonAsync(
            $"/api/posts/{post.Id}/comments", new { content = "comment", parentCommentId = (Guid?)null }));

        (await reactor.PutAsJsonAsync($"/api/posts/comments/{comment.Id}/reaction", new { type = "love" }))
            .EnsureSuccessStatusCode();
        var notifications = await ReadAsync<NotificationPageResponse>(
            await commenter.GetAsync("/api/notifications"));

        var notification = Assert.Single(notifications.Items, item => item.Type == "CommentReaction");
        Assert.Equal(comment.Id, notification.EntityId);
        Assert.Equal(post.Id, notification.ParentEntityId);
    }

    [Fact]
    public async Task Notification_cursor_ownership_and_read_operations_are_scoped_to_recipient()
    {
        var users = await CreateUserIdsAsync(2);
        var recipient = users[0];
        var otherUser = users[1];
        var now = DateTimeOffset.UtcNow;
        var oldest = Notification.Create(
            Guid.NewGuid(), recipient, otherUser, NotificationType.POST_MENTION, null, null, now.AddMinutes(-2));
        var middle = Notification.Create(
            Guid.NewGuid(), recipient, otherUser, NotificationType.POST_MENTION, null, null, now.AddMinutes(-1));
        var newest = Notification.Create(
            Guid.NewGuid(), recipient, otherUser, NotificationType.POST_MENTION, null, null, now);
        var privateNotification = Notification.Create(
            Guid.NewGuid(), otherUser, recipient, NotificationType.POST_MENTION, null, null, now);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            db.Notifications.AddRange(oldest, middle, newest, privateNotification);
            await db.SaveChangesAsync();
        }

        using var recipientClient = CreateAuthenticatedClient(recipient);
        using var otherClient = CreateAuthenticatedClient(otherUser);
        var firstPage = await ReadAsync<NotificationPageResponse>(
            await recipientClient.GetAsync("/api/notifications?limit=2"));
        var secondPage = await ReadAsync<NotificationPageResponse>(
            await recipientClient.GetAsync("/api/notifications?limit=2&before=" +
                Uri.EscapeDataString(firstPage.NextCursor!)));
        var unauthorizedRead = await otherClient.PostAsync(
            "/api/notifications/" + newest.Id + "/read",
            null);
        var countBefore = await recipientClient.GetFromJsonAsync<NotificationCountResponse>(
            "/api/notifications/unread-count");
        var read = await recipientClient.PostAsync("/api/notifications/" + newest.Id + "/read", null);
        var countAfterOne = await recipientClient.GetFromJsonAsync<NotificationCountResponse>(
            "/api/notifications/unread-count");
        var readAll = await recipientClient.PostAsync("/api/notifications/read-all", null);
        var countAfterAll = await recipientClient.GetFromJsonAsync<NotificationCountResponse>(
            "/api/notifications/unread-count");

        Assert.Equal(newest.Id, firstPage.Items[0].Id);
        Assert.Equal(middle.Id, firstPage.Items[1].Id);
        Assert.Single(secondPage.Items);
        Assert.Equal(oldest.Id, secondPage.Items[0].Id);
        Assert.Equal(HttpStatusCode.NotFound, unauthorizedRead.StatusCode);
        Assert.Equal(3, countBefore!.UnreadNotificationCount);
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
        Assert.Equal(2, countAfterOne!.UnreadNotificationCount);
        Assert.Equal(HttpStatusCode.NoContent, readAll.StatusCode);
        Assert.Equal(0, countAfterAll!.UnreadNotificationCount);
    }

    [Fact]
    public async Task Blocked_and_stale_post_notifications_are_not_returned()
    {
        var users = await CreateUserIdsAsync(3);
        var recipient = users[0];
        var actor = users[1];
        var postAuthor = users[2];
        using var postAuthorClient = CreateAuthenticatedClient(postAuthor);
        using var recipientClient = CreateAuthenticatedClient(recipient);
        var post = await CreatePostAsync(postAuthorClient, "visibility target", "public");
        var blockedNotification = Notification.Create(
            Guid.NewGuid(), recipient, actor, NotificationType.POST_REACTION,
            NotificationEntityType.POST, post.Id, DateTimeOffset.UtcNow);
        var staleNotification = Notification.Create(
            Guid.NewGuid(), recipient, postAuthor, NotificationType.POST_COMMENT,
            NotificationEntityType.POST, post.Id, DateTimeOffset.UtcNow.AddTicks(1));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            db.Notifications.AddRange(blockedNotification, staleNotification);
            await db.SaveChangesAsync();
        }

        await BlockAsync(recipient, actor);
        Assert.DoesNotContain((await ReadAsync<NotificationPageResponse>(
            await recipientClient.GetAsync("/api/notifications"))).Items,
            item => item.Id == blockedNotification.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await postAuthorClient.DeleteAsync(
            "/api/posts/" + post.Id)).StatusCode);
        var visible = await ReadAsync<NotificationPageResponse>(
            await recipientClient.GetAsync("/api/notifications"));
        var unread = await recipientClient.GetFromJsonAsync<NotificationCountResponse>(
            "/api/notifications/unread-count");

        Assert.DoesNotContain(visible.Items, item =>
            item.Id == blockedNotification.Id || item.Id == staleNotification.Id);
        Assert.Equal(0, unread!.UnreadNotificationCount);
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
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Equal(MediaStatus.DELETED,
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
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.True(await db.PostMedia.AnyAsync(x => x.PostId == imageOnly.Id && x.MediaId == own));
        var mediaDb = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
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
        var videoId = await CreateReadyMediaAsync(users[0], MediaType.VIDEO);
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
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.False(await db.PostMedia.AnyAsync(x => x.PostId == post.Id && x.MediaId == first));
        Assert.True(await db.PostMedia.AnyAsync(x => x.PostId == post.Id && x.MediaId == second));
        var mediaDb = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.False(await mediaDb.MediaReferences.AnyAsync(x => x.PostId == post.Id && x.MediaId == first));
        Assert.True(await mediaDb.MediaReferences.AnyAsync(x => x.PostId == post.Id && x.MediaId == second));
    }

    private async Task<Guid[]> CreateUserIdsAsync(int count)
    {
        var users = Enumerable.Range(0, count)
            .Select(index => new User(
                Guid.NewGuid(),
                $"posts-{Guid.NewGuid():N}@example.com",
                $"posts_{Guid.NewGuid():N}"[..32],
                DateTimeOffset.UtcNow.AddTicks(index)))
            .ToArray();
        using var scope = factory.Services.CreateScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        identityDb.Users.AddRange(users);
        await identityDb.SaveChangesAsync();
        return users.Select(user => user.Id).ToArray();
    }

    private async Task CreateProfileAsync(Guid userId, string username)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.UserProfiles.Add(new UserProfile(userId, username, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

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

    private async Task<Guid> CreateReadyMediaAsync(Guid ownerUserId, MediaType mediaType = MediaType.IMAGE)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var now = DateTimeOffset.UtcNow;
        var mediaId = Guid.NewGuid();
        var asset = MediaAsset.CreatePending(
            mediaId,
            ownerUserId,
            mediaType,
            $"{ownerUserId:N}/{mediaId:N}{(mediaType == MediaType.VIDEO ? ".mp4" : ".png")}",
            mediaType == MediaType.VIDEO ? "video.mp4" : "photo.png",
            mediaType == MediaType.VIDEO ? "video/mp4" : "image/png",
            11,
            now,
            now.AddMinutes(5));
        if (mediaType == MediaType.VIDEO)
        {
            asset.MarkProcessing(11, now);
            asset.MarkVideoReady(
                MediaAsset.ProcessedKey(ownerUserId, mediaId),
                MediaAsset.PosterKey(ownerUserId, mediaId),
                10_000,
                720,
                1280,
                now);
        }
        else
        {
            asset.MarkReady(11, now);
        }
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync();
        return mediaId;
    }

    private async Task DeleteMediaAsync(Guid mediaId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
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

    private sealed record NotificationCountResponse(int UnreadNotificationCount);

}
