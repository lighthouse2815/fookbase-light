using Fookbase.Api.Modules.Friends.Domain.Enums;
using Fookbase.Api.Modules.Groups.Domain.Enums;
using Fookbase.Api.Modules.Notifications.Domain.Enums;
using Fookbase.Api.Modules.Friends.DTOs.Responses;
using Fookbase.Api.Modules.Notifications.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Fookbase.Api.Modules.Friends.Config;
using Fookbase.Api.Modules.Friends.Entities;
using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Groups.Entities;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Pages.Entities;
using Fookbase.Api.Modules.Users.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Friends.Api.IntegrationTests;

public sealed class FriendEndpointsTests(FriendsApiFactory factory)
    : IClassFixture<FriendsApiFactory>
{
    [Fact]
    public async Task Friendship_backfill_creates_bidirectional_follows_without_duplicates()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        await EnsureEligibleUsersAsync(userA, userB);
        var followedAtUtc = new DateTimeOffset(2026, 9, 13, 13, 45, 30, 123, TimeSpan.Zero)
            .AddTicks(4_560);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var migrations = dbContext.Database.GetMigrations().ToList();
        var followMigration = Assert.Single(migrations,
            migration => migration.EndsWith("_AddUserFollowV1", StringComparison.Ordinal));
        var latestMigration = migrations[^1];
        var previousMigration = migrations[
            migrations.IndexOf(followMigration) - 1];
        var migrator = dbContext.Database.GetService<IMigrator>();

        await migrator.MigrateAsync(previousMigration);
        dbContext.Friendships.Add(Friendship.Create(Guid.NewGuid(), userA, userB, followedAtUtc));
        await dbContext.SaveChangesAsync();

        await migrator.MigrateAsync(latestMigration);

        var follows = await dbContext.UserFollows
            .Where(follow =>
                (follow.FollowerUserId == userA && follow.FollowingUserId == userB) ||
                (follow.FollowerUserId == userB && follow.FollowingUserId == userA))
            .ToListAsync();

        Assert.Equal(2, follows.Count);
        Assert.Contains(follows, follow =>
            follow.FollowerUserId == userA &&
            follow.FollowingUserId == userB &&
            follow.FollowedAtUtc == followedAtUtc);
        Assert.Contains(follows, follow =>
            follow.FollowerUserId == userB &&
            follow.FollowingUserId == userA &&
            follow.FollowedAtUtc == followedAtUtc);

        await ExecuteFriendshipBackfillSqlAsync(dbContext, followMigration);

        Assert.Equal(2, await dbContext.UserFollows.CountAsync(follow =>
            (follow.FollowerUserId == userA && follow.FollowingUserId == userB) ||
            (follow.FollowerUserId == userB && follow.FollowingUserId == userA)));
    }

    [Fact]
    public void User_follow_rejects_following_yourself()
    {
        var userId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            UserFollow.Create(userId, userId, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Follow_is_idempotent_and_creates_a_single_general_notification()
    {
        var users = CreateUserIds(2);
        var followerUserId = users[0];
        var followedUserId = users[1];
        await EnsureEligibleUsersAsync(followerUserId, followedUserId);
        using var follower = CreateAuthenticatedClient(followerUserId);

        var first = await follower.PostAsync($"/api/users/{followedUserId}/follow", null);
        var repeated = await follower.PostAsync($"/api/users/{followedUserId}/follow", null);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Equal(1, await dbContext.UserFollows.CountAsync(follow =>
            follow.FollowerUserId == followerUserId && follow.FollowingUserId == followedUserId));
        Assert.Equal(1, await dbContext.Notifications.CountAsync(notification =>
            notification.RecipientUserId == followedUserId &&
            notification.ActorUserId == followerUserId &&
            notification.Type == NotificationType.USER_FOLLOWED));

        var unfollow = await follower.DeleteAsync($"/api/users/{followedUserId}/follow");
        var repeatedUnfollow = await follower.DeleteAsync($"/api/users/{followedUserId}/follow");
        Assert.Equal(HttpStatusCode.NoContent, unfollow.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, repeatedUnfollow.StatusCode);
        Assert.Equal(0, await dbContext.UserFollows.CountAsync(follow =>
            follow.FollowerUserId == followerUserId && follow.FollowingUserId == followedUserId));
    }

    [Fact]
    public async Task Follow_rejects_self_missing_inactive_and_blocked_targets()
    {
        var users = CreateUserIds(4);
        var actorUserId = users[0];
        var inactiveUserId = users[1];
        var blockedUserId = users[2];
        var profilelessUserId = users[3];
        await EnsureEligibleUsersAsync(actorUserId, inactiveUserId, blockedUserId);
        await EnsureAccountWithoutProfileAsync(profilelessUserId);
        await DisableUserAsync(inactiveUserId);
        using var actor = CreateAuthenticatedClient(actorUserId);
        using var blocked = CreateAuthenticatedClient(blockedUserId);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await actor.PostAsync($"/api/users/{actorUserId}/follow", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await actor.PostAsync($"/api/users/{Guid.NewGuid()}/follow", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await actor.PostAsync($"/api/users/{inactiveUserId}/follow", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await actor.PostAsync($"/api/users/{profilelessUserId}/follow", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await blocked.PostAsync($"/api/friends/blocks/{actorUserId}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await actor.PostAsync($"/api/users/{blockedUserId}/follow", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await blocked.PostAsync($"/api/users/{actorUserId}/follow", null)).StatusCode);
    }

    [Fact]
    public async Task Follow_lists_use_viewer_bound_cursor_hide_blocks_and_return_counts()
    {
        var users = CreateUserIds(5);
        var ownerUserId = users[0];
        var viewerUserId = users[1];
        var firstFollowedUserId = users[2];
        var secondFollowedUserId = users[3];
        var hiddenFollowedUserId = users[4];
        await EnsureEligibleUsersAsync(users);
        using var owner = CreateAuthenticatedClient(ownerUserId);
        using var viewer = CreateAuthenticatedClient(viewerUserId);

        foreach (var targetUserId in new[] { firstFollowedUserId, secondFollowedUserId, hiddenFollowedUserId })
        {
            Assert.Equal(HttpStatusCode.NoContent,
                (await owner.PostAsync($"/api/users/{targetUserId}/follow", null)).StatusCode);
        }

        var firstPage = await owner.GetFromJsonAsync<FollowPage>(
            $"/api/users/{ownerUserId}/following?limit=2");
        Assert.NotNull(firstPage);
        Assert.Equal(3, firstPage.Total);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.False(string.IsNullOrWhiteSpace(firstPage.NextCursor));
        var secondPage = await owner.GetFromJsonAsync<FollowPage>(
            $"/api/users/{ownerUserId}/following?limit=2&cursor={Uri.EscapeDataString(firstPage.NextCursor!)}");
        Assert.NotNull(secondPage);
        Assert.Single(secondPage.Items);
        Assert.Null(secondPage.NextCursor);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.GetAsync($"/api/users/{ownerUserId}/following?cursor=not-a-cursor")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await viewer.GetAsync(
                $"/api/users/{ownerUserId}/following?cursor={Uri.EscapeDataString(firstPage.NextCursor!)}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await viewer.PostAsync($"/api/friends/blocks/{hiddenFollowedUserId}", null)).StatusCode);
        var hidden = await viewer.GetFromJsonAsync<FollowPage>(
            $"/api/users/{ownerUserId}/following?limit=10");
        Assert.NotNull(hidden);
        Assert.Equal(2, hidden.Total);
        Assert.DoesNotContain(hidden.Items, item => item.UserId == hiddenFollowedUserId);

        var followers = await owner.GetFromJsonAsync<FollowPage>(
            $"/api/users/{firstFollowedUserId}/followers?limit=10");
        Assert.NotNull(followers);
        Assert.Single(followers.Items);
        Assert.Equal(ownerUserId, followers.Items[0].UserId);
    }

    [Fact]
    public async Task Follow_lists_traverse_both_directions_with_equal_times_and_reject_cross_bound_cursors()
    {
        var userIds = CreateUserIds(8);
        var ownerUserId = userIds[0];
        var otherOwnerUserId = userIds[1];
        var followingUserIds = userIds[2..5];
        var followerUserIds = userIds[5..8];
        await EnsureEligibleUsersAsync(userIds);
        var followedAtUtc = new DateTimeOffset(2026, 9, 13, 14, 15, 16, TimeSpan.Zero);
        await AddFollowsAsync(
            followingUserIds.Select(userId => (ownerUserId, userId, followedAtUtc))
                .Concat(followerUserIds.Select(userId => (userId, ownerUserId, followedAtUtc))));
        using var owner = CreateAuthenticatedClient(ownerUserId);

        var followingFirstPage = await owner.GetFromJsonAsync<FollowPage>(
            $"/api/users/{ownerUserId}/following?limit=2");
        Assert.NotNull(followingFirstPage);
        Assert.Equal(3, followingFirstPage.Total);
        Assert.Equal(2, followingFirstPage.Items.Count);
        Assert.False(string.IsNullOrWhiteSpace(followingFirstPage.NextCursor));
        var followingSecondPage = await owner.GetFromJsonAsync<FollowPage>(
            $"/api/users/{ownerUserId}/following?limit=2&cursor={Uri.EscapeDataString(followingFirstPage.NextCursor!)}");
        Assert.NotNull(followingSecondPage);
        Assert.Equal(followingUserIds.Order(), followingFirstPage.Items
            .Concat(followingSecondPage.Items).Select(item => item.UserId).Order());

        var followersFirstPage = await owner.GetFromJsonAsync<FollowPage>(
            $"/api/users/{ownerUserId}/followers?limit=2");
        Assert.NotNull(followersFirstPage);
        Assert.Equal(3, followersFirstPage.Total);
        Assert.Equal(2, followersFirstPage.Items.Count);
        Assert.False(string.IsNullOrWhiteSpace(followersFirstPage.NextCursor));
        var followersSecondPage = await owner.GetFromJsonAsync<FollowPage>(
            $"/api/users/{ownerUserId}/followers?limit=2&cursor={Uri.EscapeDataString(followersFirstPage.NextCursor!)}");
        Assert.NotNull(followersSecondPage);
        Assert.Equal(followerUserIds.Order(), followersFirstPage.Items
            .Concat(followersSecondPage.Items).Select(item => item.UserId).Order());

        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync(
            $"/api/users/{otherOwnerUserId}/following?cursor={Uri.EscapeDataString(followingFirstPage.NextCursor!)}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync(
            $"/api/users/{ownerUserId}/followers?cursor={Uri.EscapeDataString(followingFirstPage.NextCursor!)}")).StatusCode);
    }

    [Fact]
    public async Task Follow_lists_do_not_enumerate_missing_ineligible_or_blocked_owners()
    {
        var userIds = CreateUserIds(4);
        var viewerUserId = userIds[0];
        var ownerUserId = userIds[1];
        var followedUserId = userIds[2];
        var profilelessOwnerUserId = userIds[3];
        await EnsureEligibleUsersAsync(viewerUserId, ownerUserId, followedUserId);
        await EnsureAccountWithoutProfileAsync(profilelessOwnerUserId);
        await AddFollowsAsync([
            (ownerUserId, followedUserId, DateTimeOffset.UtcNow),
            (profilelessOwnerUserId, followedUserId, DateTimeOffset.UtcNow)
        ]);
        using var viewer = CreateAuthenticatedClient(viewerUserId);

        Assert.Equal(HttpStatusCode.NoContent,
            (await viewer.PostAsync($"/api/friends/blocks/{ownerUserId}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await viewer.GetAsync($"/api/users/{ownerUserId}/following")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await viewer.DeleteAsync($"/api/friends/blocks/{ownerUserId}")).StatusCode);
        using var owner = CreateAuthenticatedClient(ownerUserId);
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.PostAsync($"/api/friends/blocks/{viewerUserId}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await viewer.GetAsync($"/api/users/{ownerUserId}/following")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.DeleteAsync($"/api/friends/blocks/{viewerUserId}")).StatusCode);
        await DisableUserAsync(ownerUserId);
        Assert.Equal(HttpStatusCode.NotFound,
            (await viewer.GetAsync($"/api/users/{ownerUserId}/following")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await viewer.GetAsync($"/api/users/{profilelessOwnerUserId}/following")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await viewer.GetAsync($"/api/users/{Guid.NewGuid()}/following")).StatusCode);
    }

    [Fact]
    public async Task Accept_creates_mutual_follows_without_follow_notification_and_block_deletes_them()
    {
        var users = CreateUserIds(2);
        await EnsureEligibleUsersAsync(users);
        var userA = users[0];
        var userB = users[1];
        var request = await SendRequestAsync(userA, userB);
        using var clientA = CreateAuthenticatedClient(userA);
        using var clientB = CreateAuthenticatedClient(userB);

        Assert.Equal(HttpStatusCode.OK,
            (await clientB.PostAsync($"/api/friends/requests/{request.Id}/accept", null)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            Assert.Equal(2, await dbContext.UserFollows.CountAsync(follow =>
                (follow.FollowerUserId == userA && follow.FollowingUserId == userB) ||
                (follow.FollowerUserId == userB && follow.FollowingUserId == userA)));
            Assert.Equal(0, await dbContext.Notifications.CountAsync(notification =>
                notification.Type == NotificationType.USER_FOLLOWED &&
                ((notification.RecipientUserId == userA && notification.ActorUserId == userB) ||
                 (notification.RecipientUserId == userB && notification.ActorUserId == userA))));
        }

        Assert.Equal(HttpStatusCode.NoContent,
            (await clientA.DeleteAsync($"/api/friends/{userB}")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            Assert.Equal(2, await dbContext.UserFollows.CountAsync(follow =>
                (follow.FollowerUserId == userA && follow.FollowingUserId == userB) ||
                (follow.FollowerUserId == userB && follow.FollowingUserId == userA)));
        }

        Assert.Equal(HttpStatusCode.NoContent,
            (await clientA.PostAsync($"/api/friends/blocks/{userB}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await clientA.DeleteAsync($"/api/friends/blocks/{userB}")).StatusCode);
        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Equal(0, await verificationDbContext.UserFollows.CountAsync(follow =>
            (follow.FollowerUserId == userA && follow.FollowingUserId == userB) ||
            (follow.FollowerUserId == userB && follow.FollowingUserId == userA)));
    }

    [Fact]
    public async Task Missing_jwt_returns_unauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/friends");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Send_request_rejects_self_duplicate_and_reverse_pending_request()
    {
        var users = CreateUserIds(2);
        await EnsureEligibleUsersAsync(users);
        var userA = users[0];
        var userB = users[1];
        using var clientA = CreateAuthenticatedClient(userA);
        using var clientB = CreateAuthenticatedClient(userB);

        var self = await clientA.PostAsync($"/api/friends/requests/{userA}", null);
        var first = await clientA.PostAsync($"/api/friends/requests/{userB}", null);
        var duplicate = await clientA.PostAsync($"/api/friends/requests/{userB}", null);
        var reverse = await clientB.PostAsync($"/api/friends/requests/{userA}", null);

        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, reverse.StatusCode);

        var request = await first.Content.ReadFromJsonAsync<FriendRequestResponse>();
        Assert.NotNull(request);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Equal(1, await dbContext.FriendRequests.CountAsync(
            item => item.UserId1 == Min(userA, userB) &&
                    item.UserId2 == Max(userA, userB) &&
                    item.Status == FriendRequestStatus.PENDING));
    }

    [Fact]
    public async Task Concurrent_reverse_requests_create_only_one_pending_relationship()
    {
        var users = CreateUserIds(2);
        await EnsureEligibleUsersAsync(users);
        var userA = users[0];
        var userB = users[1];
        using var clientA = CreateAuthenticatedClient(userA);
        using var clientB = CreateAuthenticatedClient(userB);

        var responses = await Task.WhenAll(
            clientA.PostAsync($"/api/friends/requests/{userB}", null),
            clientB.PostAsync($"/api/friends/requests/{userA}", null));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Equal(1, await dbContext.FriendRequests.CountAsync(
            item => item.UserId1 == Min(userA, userB) &&
                    item.UserId2 == Max(userA, userB) &&
                    item.Status == FriendRequestStatus.PENDING));
    }

    [Fact]
    public async Task Only_receiver_can_accept_and_concurrent_accept_creates_one_friendship()
    {
        var users = CreateUserIds(3);
        await EnsureEligibleUsersAsync(users);
        var userA = users[0];
        var userB = users[1];
        var userC = users[2];
        var request = await SendRequestAsync(userA, userB);
        using var clientA = CreateAuthenticatedClient(userA);
        using var clientB1 = CreateAuthenticatedClient(userB);
        using var clientB2 = CreateAuthenticatedClient(userB);
        using var clientC = CreateAuthenticatedClient(userC);

        var senderAccept = await clientA.PostAsync(
            $"/api/friends/requests/{request.Id}/accept",
            null);
        var outsiderAccept = await clientC.PostAsync(
            $"/api/friends/requests/{request.Id}/accept",
            null);
        var concurrent = await Task.WhenAll(
            clientB1.PostAsync($"/api/friends/requests/{request.Id}/accept", null),
            clientB2.PostAsync($"/api/friends/requests/{request.Id}/accept", null));

        Assert.Equal(HttpStatusCode.Forbidden, senderAccept.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, outsiderAccept.StatusCode);
        Assert.Single(concurrent, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(concurrent, response => response.StatusCode == HttpStatusCode.Conflict);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Equal(1, await dbContext.Friendships.CountAsync(
            item => item.UserId1 == Min(userA, userB) && item.UserId2 == Max(userA, userB)));
    }

    [Fact]
    public async Task Receiver_can_decline_and_sender_can_cancel()
    {
        var users = CreateUserIds(3);
        await EnsureEligibleUsersAsync(users);
        var declinedRequest = await SendRequestAsync(users[0], users[1]);
        var cancelledRequest = await SendRequestAsync(users[0], users[2]);
        using var receiver = CreateAuthenticatedClient(users[1]);
        using var sender = CreateAuthenticatedClient(users[0]);

        var declined = await receiver.PostAsync(
            $"/api/friends/requests/{declinedRequest.Id}/decline",
            null);
        var cancelled = await sender.DeleteAsync(
            $"/api/friends/requests/{cancelledRequest.Id}");

        Assert.Equal(HttpStatusCode.NoContent, declined.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Equal(FriendRequestStatus.DECLINED,
            (await dbContext.FriendRequests.FindAsync(declinedRequest.Id))!.Status);
        Assert.Equal(FriendRequestStatus.CANCELLED,
            (await dbContext.FriendRequests.FindAsync(cancelledRequest.Id))!.Status);
    }

    [Fact]
    public async Task Lists_and_status_reflect_pending_friendship_and_unfriend()
    {
        var users = CreateUserIds(2);
        await EnsureEligibleUsersAsync(users);
        var userA = users[0];
        var userB = users[1];
        var request = await SendRequestAsync(userA, userB);
        using var clientA = CreateAuthenticatedClient(userA);
        using var clientB = CreateAuthenticatedClient(userB);

        var outgoing = await clientA.GetFromJsonAsync<PagedResponse<FriendRequestResponse>>(
            "/api/friends/requests/outgoing");
        var incoming = await clientB.GetFromJsonAsync<PagedResponse<FriendRequestResponse>>(
            "/api/friends/requests/incoming");
        var pendingStatus = await clientA.GetFromJsonAsync<RelationshipStatusResponse>(
            $"/api/friends/status/{userB}");
        var receiverNotifications = await clientB.GetFromJsonAsync<PagedResponse<FriendNotificationResponse>>(
            "/api/friends/notifications/unread");
        Assert.Contains(outgoing!.Items, item => item.Id == request.Id);
        Assert.Contains(incoming!.Items, item => item.Id == request.Id);
        Assert.Equal("request_sent", pendingStatus!.Status);
        var requestNotification = Assert.Single(receiverNotifications!.Items);
        Assert.Equal(userA, requestNotification.ActorUserId);
        Assert.Equal("friend_request", requestNotification.Type);

        var markRead = await clientB.PostAsync($"/api/friends/notifications/{requestNotification.Id}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, markRead.StatusCode);
        Assert.Empty((await clientB.GetFromJsonAsync<PagedResponse<FriendNotificationResponse>>(
            "/api/friends/notifications/unread"))!.Items);

        var accepted = await clientB.PostAsync(
            $"/api/friends/requests/{request.Id}/accept",
            null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var friendsA = await clientA.GetFromJsonAsync<PagedResponse<FriendResponse>>("/api/friends");
        var friendsB = await clientB.GetFromJsonAsync<PagedResponse<FriendResponse>>("/api/friends");
        Assert.Contains(friendsA!.Items, item => item.UserId == userB);
        Assert.Contains(friendsB!.Items, item => item.UserId == userA);
        Assert.Equal("friends", (await clientA.GetFromJsonAsync<RelationshipStatusResponse>(
            $"/api/friends/status/{userB}"))!.Status);
        var senderNotifications = await clientA.GetFromJsonAsync<PagedResponse<FriendNotificationResponse>>(
            "/api/friends/notifications/unread");
        var acceptanceNotification = Assert.Single(senderNotifications!.Items);
        Assert.Equal(userB, acceptanceNotification.ActorUserId);
        Assert.Equal("friend_accepted", acceptanceNotification.Type);

        var unfriend = await clientA.DeleteAsync($"/api/friends/{userB}");
        Assert.Equal(HttpStatusCode.NoContent, unfriend.StatusCode);
        Assert.Equal("none", (await clientA.GetFromJsonAsync<RelationshipStatusResponse>(
            $"/api/friends/status/{userB}"))!.Status);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
    }

    [Fact]
    public async Task Block_removes_friendship_cancels_pending_and_prevents_both_directions()
    {
        var users = CreateUserIds(3);
        await EnsureEligibleUsersAsync(users);
        var userA = users[0];
        var userB = users[1];
        var userC = users[2];
        await BecomeFriendsAsync(userA, userB);
        var pending = await SendRequestAsync(userC, userA);
        using var clientA = CreateAuthenticatedClient(userA);
        using var clientB = CreateAuthenticatedClient(userB);
        using var clientC = CreateAuthenticatedClient(userC);

        var blockFriend = await clientA.PostAsync($"/api/friends/blocks/{userB}", null);
        var blockPending = await clientA.PostAsync($"/api/friends/blocks/{userC}", null);

        Assert.Equal(HttpStatusCode.NoContent, blockFriend.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, blockPending.StatusCode);
        Assert.Equal("blocked", (await clientA.GetFromJsonAsync<RelationshipStatusResponse>(
            $"/api/friends/status/{userB}"))!.Status);
        Assert.Equal(HttpStatusCode.Conflict,
            (await clientA.PostAsync($"/api/friends/requests/{userB}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await clientB.PostAsync($"/api/friends/requests/{userA}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await clientC.PostAsync($"/api/friends/requests/{userA}", null)).StatusCode);

        var blocked = await clientA.GetFromJsonAsync<PagedResponse<BlockedUserResponse>>(
            "/api/friends/blocks");
        Assert.Contains(blocked!.Items, item => item.UserId == userB);
        Assert.Contains(blocked.Items, item => item.UserId == userC);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.False(await dbContext.Friendships.AnyAsync(
            item => item.UserId1 == Min(userA, userB) && item.UserId2 == Max(userA, userB)));
        Assert.Equal(FriendRequestStatus.CANCELLED,
            (await dbContext.FriendRequests.FindAsync(pending.Id))!.Status);
    }

    [Fact]
    public async Task Unblock_does_not_restore_friendship()
    {
        var users = CreateUserIds(2);
        await EnsureEligibleUsersAsync(users);
        var userA = users[0];
        var userB = users[1];
        await BecomeFriendsAsync(userA, userB);
        using var clientA = CreateAuthenticatedClient(userA);

        await clientA.PostAsync($"/api/friends/blocks/{userB}", null);
        var unblock = await clientA.DeleteAsync($"/api/friends/blocks/{userB}");

        Assert.Equal(HttpStatusCode.NoContent, unblock.StatusCode);
        var status = await clientA.GetFromJsonAsync<RelationshipStatusResponse>(
            $"/api/friends/status/{userB}");
        Assert.Equal("none", status!.Status);
        var friends = await clientA.GetFromJsonAsync<PagedResponse<FriendResponse>>("/api/friends");
        Assert.DoesNotContain(friends!.Items, item => item.UserId == userB);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
    }

    [Fact]
    public async Task Mutual_friends_returns_intersection_from_friends_database()
    {
        var users = CreateUserIds(4);
        await EnsureEligibleUsersAsync(users);
        var userA = users[0];
        var userB = users[1];
        var mutual1 = users[2];
        var mutual2 = users[3];
        await BecomeFriendsAsync(userA, mutual1);
        await BecomeFriendsAsync(userB, mutual1);
        await BecomeFriendsAsync(userA, mutual2);
        await BecomeFriendsAsync(userB, mutual2);
        using var clientA = CreateAuthenticatedClient(userA);

        var response = await clientA.GetFromJsonAsync<MutualFriendsResponse>(
            $"/api/friends/mutual/{userB}?limit=1");

        Assert.NotNull(response);
        Assert.Equal(2, response.Count);
        Assert.Single(response.UserIds);
        Assert.Contains(response.UserIds[0], new[] { mutual1, mutual2 });
    }

    [Fact]
    public async Task Pagination_rejects_excessive_limit()
    {
        var user = CreateUserIds(1)[0];
        using var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/friends?limit=101");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/friends", "offset=-1", "Offset")]
    [InlineData("/api/friends", "limit=0", "Limit")]
    [InlineData("/api/friends/requests/incoming", "limit=101", "Limit")]
    [InlineData("/api/friends/requests/outgoing", "offset=-1", "Offset")]
    [InlineData("/api/friends/notifications/unread", "limit=0", "Limit")]
    [InlineData("/api/friends/blocks", "offset=-1", "Offset")]
    [InlineData("/api/friends/mutual/{userId}", "limit=101", "Limit")]
    [InlineData("/api/friends/suggestions", "limit=51", "Limit")]
    [InlineData("/api/friends/suggestions", "cursor=not-a-cursor", "Cursor")]
    [InlineData("/api/users/{userId}/friends", "offset=-1", "Offset")]
    [InlineData("/api/users/{userId}/followers", "limit=101", "Limit")]
    [InlineData("/api/users/{userId}/following", "limit=0", "Limit")]
    [InlineData("/api/users/{userId}/followers", "cursor=not-a-cursor", "Cursor")]
    [InlineData("/api/users/{userId}/following", "cursor=not-a-cursor", "Cursor")]
    public async Task Invalid_relationship_queries_return_request_validation_errors(
        string path, string query, string field)
    {
        var userIds = CreateUserIds(2);
        await EnsureEligibleUsersAsync(userIds);
        using var client = CreateAuthenticatedClient(userIds[0]);
        using var response = await client.GetAsync(
            $"{path.Replace("{userId}", userIds[1].ToString())}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty(field, out var messages));
        Assert.NotEmpty(messages.EnumerateArray());
    }

    [Fact]
    public async Task Suggestion_request_uses_configured_default_and_maximum_page_sizes()
    {
        var userIds = CreateUserIds(5);
        await EnsureEligibleUsersAsync(userIds);
        await SeedSharedGroupAsync(userIds[0], userIds.Skip(1), "configured-suggestion-page");
        using var authenticated = CreateAuthenticatedClient(userIds[0]);
        using var configuredFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.Remove(services.Single(descriptor => descriptor.ServiceType == typeof(FriendSuggestionOptions)));
            services.AddSingleton(new FriendSuggestionOptions { DefaultPageSize = 2, MaximumPageSize = 3 });
        }));
        using var client = configuredFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = authenticated.DefaultRequestHeaders.Authorization;

        var defaultPage = await client.GetFromJsonAsync<SuggestionPage>("/api/friends/suggestions");
        Assert.Equal(2, defaultPage!.Items.Count);
        var maximumPage = await client.GetFromJsonAsync<SuggestionPage>("/api/friends/suggestions?limit=3");
        Assert.Equal(3, maximumPage!.Items.Count);
        using var invalidResponse = await client.GetAsync("/api/friends/suggestions?limit=4");
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        using var payload = JsonDocument.Parse(await invalidResponse.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.GetProperty("errors").TryGetProperty("Limit", out _));
    }

    [Fact]
    public async Task Suggestions_rank_independent_sources_with_configurable_weights_and_a_viewer_bound_cursor()
    {
        var ids = CreateSortedUserIds(6);
        var pageCandidateUserId = ids[0];
        var firstGroupCandidateUserId = ids[1];
        var secondGroupCandidateUserId = ids[2];
        var mutualCandidateUserId = ids[3];
        var mutualUserId = ids[4];
        var viewerUserId = ids[5];
        await EnsureEligibleUsersAsync(ids);
        var secrets = await SeedRankedSuggestionGraphAsync(
            viewerUserId,
            mutualUserId,
            mutualCandidateUserId,
            firstGroupCandidateUserId,
            secondGroupCandidateUserId,
            pageCandidateUserId);
        using var viewer = CreateAuthenticatedClient(viewerUserId);
        using var anotherViewer = CreateAuthenticatedClient(mutualUserId);

        using var firstResponse = await viewer.GetAsync("/api/friends/suggestions?limit=2");
        firstResponse.EnsureSuccessStatusCode();
        var firstPayload = await firstResponse.Content.ReadAsStringAsync();
        var firstPage = DeserializeSuggestionPage(firstPayload);

        Assert.Equal(4, firstPage.Total);
        Assert.Equal(
            new[] { mutualCandidateUserId, firstGroupCandidateUserId },
            firstPage.Items.Select(item => item.Profile.UserId));
        var mutualCandidate = firstPage.Items[0];
        Assert.Equal(1, mutualCandidate.MutualFriendCount);
        Assert.Equal(0, mutualCandidate.SharedGroupCount);
        Assert.Equal(0, mutualCandidate.SharedPageCount);
        Assert.Equal("none", mutualCandidate.RelationshipStatus);
        Assert.False(mutualCandidate.IsFollowing);
        var groupCandidate = firstPage.Items[1];
        Assert.Equal(0, groupCandidate.MutualFriendCount);
        Assert.Equal(1, groupCandidate.SharedGroupCount);
        Assert.Equal(0, groupCandidate.SharedPageCount);
        Assert.False(firstPayload.Contains(secrets.SharedGroupName, StringComparison.Ordinal));
        Assert.False(firstPayload.Contains(secrets.SharedPageName, StringComparison.Ordinal));
        Assert.False(firstPayload.Contains("groupId", StringComparison.OrdinalIgnoreCase));
        Assert.False(firstPayload.Contains("pageId", StringComparison.OrdinalIgnoreCase));
        Assert.False(firstPayload.Contains("followerUserId", StringComparison.OrdinalIgnoreCase));
        Assert.False(firstPayload.Contains("followingUserId", StringComparison.OrdinalIgnoreCase));
        Assert.False(string.IsNullOrWhiteSpace(firstPage.NextCursor));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await viewer.GetAsync("/api/friends/suggestions?cursor=not-a-cursor")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await anotherViewer.GetAsync($"/api/friends/suggestions?cursor={Uri.EscapeDataString(firstPage.NextCursor!)}")).StatusCode);

        var secondRequest = $"/api/friends/suggestions?limit=2&cursor={Uri.EscapeDataString(firstPage.NextCursor!)}";
        var secondPage = await viewer.GetFromJsonAsync<SuggestionPage>(secondRequest);
        var unchangedGraphPage = await viewer.GetFromJsonAsync<SuggestionPage>(secondRequest);

        Assert.NotNull(secondPage);
        Assert.NotNull(unchangedGraphPage);
        Assert.Equal(
            new[] { secondGroupCandidateUserId, pageCandidateUserId },
            secondPage.Items.Select(item => item.Profile.UserId));
        Assert.Equal(1, secondPage.Items[1].SharedPageCount);
        Assert.Equal(secondPage.Items, unchangedGraphPage.Items);
        Assert.Null(secondPage.NextCursor);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var protectionProvider = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        var configuredService = new FriendSuggestionService(
            dbContext,
            protectionProvider,
            new FriendSuggestionOptions
            {
                CandidateLimitPerSource = 51,
                MutualFriendWeight = 1,
                SharedGroupWeight = 2,
                SharedPageWeight = 5
            });

        var configuredResult = await configuredService.GetSuggestionsAsync(viewerUserId, null, 50);

        Assert.True(configuredResult.Succeeded);
        Assert.Equal(
            new[]
            {
                pageCandidateUserId,
                firstGroupCandidateUserId,
                secondGroupCandidateUserId,
                mutualCandidateUserId
            },
            configuredResult.Value!.Items.Select(item => item.Profile.UserId));
    }

    [Fact]
    public async Task Suggestions_default_to_twenty_and_allow_the_maximum_fifty_limit()
    {
        var emptyViewerUserId = Guid.NewGuid();
        await EnsureEligibleUsersAsync(emptyViewerUserId);
        using var emptyViewer = CreateAuthenticatedClient(emptyViewerUserId);
        var emptyPage = await emptyViewer.GetFromJsonAsync<SuggestionPage>("/api/friends/suggestions");

        Assert.NotNull(emptyPage);
        Assert.Empty(emptyPage.Items);
        Assert.Equal(0, emptyPage.Total);
        Assert.Null(emptyPage.NextCursor);

        var userIds = CreateUserIds(22);
        var viewerUserId = userIds[0];
        var candidateUserIds = userIds[1..];
        await EnsureEligibleUsersAsync(userIds);
        await SeedSharedGroupAsync(viewerUserId, candidateUserIds, "default-limit-private-group");
        using var viewer = CreateAuthenticatedClient(viewerUserId);

        var defaultPage = await viewer.GetFromJsonAsync<SuggestionPage>("/api/friends/suggestions");
        var maximumPage = await viewer.GetFromJsonAsync<SuggestionPage>("/api/friends/suggestions?limit=50");

        Assert.NotNull(defaultPage);
        Assert.Equal(21, defaultPage.Total);
        Assert.Equal(20, defaultPage.Items.Count);
        Assert.False(string.IsNullOrWhiteSpace(defaultPage.NextCursor));
        Assert.NotNull(maximumPage);
        Assert.Equal(21, maximumPage.Total);
        Assert.Equal(21, maximumPage.Items.Count);
        Assert.Null(maximumPage.NextCursor);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await viewer.GetAsync("/api/friends/suggestions?limit=51")).StatusCode);
    }

    [Fact]
    public async Task Suggestions_exclude_incoming_blocks_ineligible_profiles_and_inactive_sources()
    {
        var userIds = CreateUserIds(10);
        var viewerUserId = userIds[0];
        var eligibleCandidateUserId = userIds[1];
        var existingFriendUserId = userIds[2];
        var outgoingPendingUserId = userIds[3];
        var incomingPendingUserId = userIds[4];
        var incomingBlockerUserId = userIds[5];
        var inactiveUserId = userIds[6];
        var profilelessUserId = userIds[7];
        var deletedGroupCandidateUserId = userIds[8];
        var unpublishedPageCandidateUserId = userIds[9];
        await EnsureEligibleUsersAsync(
            viewerUserId,
            eligibleCandidateUserId,
            existingFriendUserId,
            outgoingPendingUserId,
            incomingPendingUserId,
            incomingBlockerUserId,
            inactiveUserId,
            deletedGroupCandidateUserId,
            unpublishedPageCandidateUserId);
        await EnsureAccountWithoutProfileAsync(profilelessUserId);
        await DisableUserAsync(inactiveUserId);
        var secrets = await SeedExcludedSuggestionGraphAsync(
            viewerUserId,
            eligibleCandidateUserId,
            existingFriendUserId,
            outgoingPendingUserId,
            incomingPendingUserId,
            incomingBlockerUserId,
            inactiveUserId,
            profilelessUserId,
            deletedGroupCandidateUserId,
            unpublishedPageCandidateUserId);
        using var viewer = CreateAuthenticatedClient(viewerUserId);

        using var response = await viewer.GetAsync("/api/friends/suggestions?limit=50");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync();
        var page = DeserializeSuggestionPage(payload);

        Assert.Equal(1, page.Total);
        Assert.Equal(new[] { eligibleCandidateUserId }, page.Items.Select(item => item.Profile.UserId));
        Assert.False(payload.Contains(secrets.ActiveGroupName, StringComparison.Ordinal));
        Assert.False(payload.Contains(secrets.DeletedGroupName, StringComparison.Ordinal));
        Assert.False(payload.Contains(secrets.UnpublishedPageName, StringComparison.Ordinal));
        Assert.False(payload.Contains("groupId", StringComparison.OrdinalIgnoreCase));
        Assert.False(payload.Contains("pageId", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<FriendRequestResponse> SendRequestAsync(Guid senderUserId, Guid receiverUserId)
    {
        using var client = CreateAuthenticatedClient(senderUserId);
        var response = await client.PostAsync($"/api/friends/requests/{receiverUserId}", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FriendRequestResponse>()
            ?? throw new InvalidOperationException("Friend request response was empty.");
    }

    private async Task BecomeFriendsAsync(Guid senderUserId, Guid receiverUserId)
    {
        var request = await SendRequestAsync(senderUserId, receiverUserId);
        using var receiver = CreateAuthenticatedClient(receiverUserId);
        var response = await receiver.PostAsync(
            $"/api/friends/requests/{request.Id}/accept",
            null);
        response.EnsureSuccessStatusCode();
    }

    private async Task EnsureEligibleUsersAsync(params Guid[] userIds)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var now = DateTimeOffset.UtcNow;
        foreach (var userId in userIds)
        {
            dbContext.Users.Add(new User(userId, $"{userId:N}@test.local", userId.ToString("N"), now));
            dbContext.UserProfiles.Add(new UserProfile(userId, userId.ToString("N"), now));
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task EnsureAccountWithoutProfileAsync(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        dbContext.Users.Add(new User(
            userId,
            $"{userId:N}@test.local",
            userId.ToString("N"),
            DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    private async Task AddFollowsAsync(
        IEnumerable<(Guid FollowerUserId, Guid FollowingUserId, DateTimeOffset FollowedAtUtc)> follows)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        foreach (var follow in follows)
        {
            dbContext.UserFollows.Add(UserFollow.Create(
                follow.FollowerUserId,
                follow.FollowingUserId,
                follow.FollowedAtUtc));
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task<SuggestionGraphSecrets> SeedRankedSuggestionGraphAsync(
        Guid viewerUserId,
        Guid mutualUserId,
        Guid mutualCandidateUserId,
        Guid firstGroupCandidateUserId,
        Guid secondGroupCandidateUserId,
        Guid pageCandidateUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var now = DateTimeOffset.UtcNow;
        var sharedGroupName = $"private-group-{Guid.NewGuid():N}";
        var sharedPageName = $"private-page-{Guid.NewGuid():N}";
        var sharedGroup = new Group(
            Guid.NewGuid(), sharedGroupName, null, GroupPrivacy.PRIVATE, viewerUserId, now);
        var sharedPage = Page.Create(
            Guid.NewGuid(),
            sharedPageName,
            $"suggestion_{Guid.NewGuid():N}",
            "Test",
            null,
            viewerUserId,
            now);
        sharedPage.Publish(now);

        dbContext.Friendships.AddRange(
            Friendship.Create(Guid.NewGuid(), viewerUserId, mutualUserId, now),
            Friendship.Create(Guid.NewGuid(), mutualUserId, mutualCandidateUserId, now));
        dbContext.Groups.Add(sharedGroup);
        dbContext.GroupMembers.AddRange(
            new GroupMember(sharedGroup.Id, viewerUserId, GroupMemberRole.OWNER, now),
            new GroupMember(sharedGroup.Id, firstGroupCandidateUserId, GroupMemberRole.MEMBER, now),
            new GroupMember(sharedGroup.Id, secondGroupCandidateUserId, GroupMemberRole.MEMBER, now));
        dbContext.Pages.Add(sharedPage);
        dbContext.PageFollowers.AddRange(
            PageFollower.Create(sharedPage.Id, viewerUserId, now),
            PageFollower.Create(sharedPage.Id, pageCandidateUserId, now));
        await dbContext.SaveChangesAsync();

        return new SuggestionGraphSecrets(sharedGroupName, sharedPageName);
    }

    private async Task SeedSharedGroupAsync(
        Guid viewerUserId,
        IEnumerable<Guid> candidateUserIds,
        string namePrefix)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var now = DateTimeOffset.UtcNow;
        var group = new Group(
            Guid.NewGuid(),
            $"{namePrefix}-{Guid.NewGuid():N}",
            null,
            GroupPrivacy.PRIVATE,
            viewerUserId,
            now);
        dbContext.Groups.Add(group);
        dbContext.GroupMembers.Add(new GroupMember(group.Id, viewerUserId, GroupMemberRole.OWNER, now));
        dbContext.GroupMembers.AddRange(candidateUserIds.Select(userId =>
            new GroupMember(group.Id, userId, GroupMemberRole.MEMBER, now)));
        await dbContext.SaveChangesAsync();
    }

    private async Task<ExcludedSuggestionGraphSecrets> SeedExcludedSuggestionGraphAsync(
        Guid viewerUserId,
        Guid eligibleCandidateUserId,
        Guid existingFriendUserId,
        Guid outgoingPendingUserId,
        Guid incomingPendingUserId,
        Guid incomingBlockerUserId,
        Guid inactiveUserId,
        Guid profilelessUserId,
        Guid deletedGroupCandidateUserId,
        Guid unpublishedPageCandidateUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var now = DateTimeOffset.UtcNow;
        var activeGroupName = $"active-private-group-{Guid.NewGuid():N}";
        var deletedGroupName = $"deleted-private-group-{Guid.NewGuid():N}";
        var unpublishedPageName = $"unpublished-private-page-{Guid.NewGuid():N}";
        var activeGroup = new Group(
            Guid.NewGuid(), activeGroupName, null, GroupPrivacy.PRIVATE, viewerUserId, now);
        var deletedGroup = new Group(
            Guid.NewGuid(), deletedGroupName, null, GroupPrivacy.PRIVATE, viewerUserId, now);
        deletedGroup.Delete(now);
        var unpublishedPage = Page.Create(
            Guid.NewGuid(),
            unpublishedPageName,
            $"unpublished_{Guid.NewGuid():N}",
            "Test",
            null,
            viewerUserId,
            now);

        dbContext.Friendships.Add(Friendship.Create(Guid.NewGuid(), viewerUserId, existingFriendUserId, now));
        dbContext.FriendRequests.AddRange(
            FriendRequest.Create(Guid.NewGuid(), viewerUserId, outgoingPendingUserId, now),
            FriendRequest.Create(Guid.NewGuid(), incomingPendingUserId, viewerUserId, now));
        dbContext.BlockedUsers.Add(BlockedUser.Create(incomingBlockerUserId, viewerUserId, now));
        dbContext.Groups.AddRange(activeGroup, deletedGroup);
        dbContext.GroupMembers.AddRange(
            new GroupMember(activeGroup.Id, viewerUserId, GroupMemberRole.OWNER, now),
            new GroupMember(activeGroup.Id, eligibleCandidateUserId, GroupMemberRole.MEMBER, now),
            new GroupMember(activeGroup.Id, existingFriendUserId, GroupMemberRole.MEMBER, now),
            new GroupMember(activeGroup.Id, outgoingPendingUserId, GroupMemberRole.MEMBER, now),
            new GroupMember(activeGroup.Id, incomingPendingUserId, GroupMemberRole.MEMBER, now),
            new GroupMember(activeGroup.Id, incomingBlockerUserId, GroupMemberRole.MEMBER, now),
            new GroupMember(activeGroup.Id, inactiveUserId, GroupMemberRole.MEMBER, now),
            new GroupMember(activeGroup.Id, profilelessUserId, GroupMemberRole.MEMBER, now),
            new GroupMember(deletedGroup.Id, viewerUserId, GroupMemberRole.OWNER, now),
            new GroupMember(deletedGroup.Id, deletedGroupCandidateUserId, GroupMemberRole.MEMBER, now));
        dbContext.Pages.Add(unpublishedPage);
        dbContext.PageFollowers.AddRange(
            PageFollower.Create(unpublishedPage.Id, viewerUserId, now),
            PageFollower.Create(unpublishedPage.Id, unpublishedPageCandidateUserId, now));
        await dbContext.SaveChangesAsync();

        return new ExcludedSuggestionGraphSecrets(activeGroupName, deletedGroupName, unpublishedPageName);
    }

    private async Task DisableUserAsync(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var user = await dbContext.Users.SingleAsync(item => item.Id == userId);
        user.Disable();
        await dbContext.SaveChangesAsync();
    }

    private static Guid[] CreateUserIds(int count) =>
        Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();

    private static Guid[] CreateSortedUserIds(int count) =>
        CreateUserIds(count).OrderBy(userId => userId).ToArray();

    private static SuggestionPage DeserializeSuggestionPage(string payload) =>
        JsonSerializer.Deserialize<SuggestionPage>(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidOperationException("Suggestion response was empty.");

    private static async Task ExecuteFriendshipBackfillSqlAsync(
        FookbaseDbContext dbContext,
        string followMigration)
    {
        var migrationsAssembly = dbContext.Database.GetService<IMigrationsAssembly>();
        var migration = migrationsAssembly.CreateMigration(
            migrationsAssembly.Migrations[followMigration],
            dbContext.Database.ProviderName!);
        var backfillSql = Assert.Single(migration.UpOperations.OfType<SqlOperation>()).Sql;

        await dbContext.Database.ExecuteSqlRawAsync(backfillSql);
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

    private static Guid Min(Guid first, Guid second) => first.CompareTo(second) < 0 ? first : second;

    private static Guid Max(Guid first, Guid second) => first.CompareTo(second) > 0 ? first : second;

    private sealed record FollowPage(
        IReadOnlyList<FollowProfile> Items,
        string? NextCursor,
        int Total);

    private sealed record FollowProfile(Guid UserId);

    private sealed record SuggestionPage(
        IReadOnlyList<SuggestionItem> Items,
        string? NextCursor,
        int Total);

    private sealed record SuggestionItem(
        SuggestionProfile Profile,
        int MutualFriendCount,
        int SharedGroupCount,
        int SharedPageCount,
        string RelationshipStatus,
        bool IsFollowing);

    private sealed record SuggestionProfile(Guid UserId);

    private sealed record SuggestionGraphSecrets(string SharedGroupName, string SharedPageName);

    private sealed record ExcludedSuggestionGraphSecrets(
        string ActiveGroupName,
        string DeletedGroupName,
        string UnpublishedPageName);
}
