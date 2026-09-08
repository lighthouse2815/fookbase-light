using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Fookbase.Api.Shared.Contracts.Friends;
using Fookbase.Api.Shared.Contracts.Identity;
using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Friends.Models;
using Fookbase.Api.Modules.Friends.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Friends.Api.IntegrationTests;

public sealed class FriendEndpointsTests(FriendsApiFactory factory)
    : IClassFixture<FriendsApiFactory>
{
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
        var users = await CreateKnownUsersAsync(2);
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
        var dbContext = scope.ServiceProvider.GetRequiredService<FriendsDbContext>();
        Assert.Equal(1, await dbContext.FriendRequests.CountAsync(
            item => item.UserId1 == Min(userA, userB) &&
                    item.UserId2 == Max(userA, userB) &&
                    item.Status == FriendRequestStatus.Pending));
        var outboxMessages = await dbContext.OutboxMessages.AsNoTracking().ToListAsync();
        Assert.Equal(1, outboxMessages.Count(
            item => item.Type == FriendRequestSentIntegrationEvent.EventType &&
                    item.Payload.Contains(request.Id.ToString(), StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Concurrent_reverse_requests_create_only_one_pending_relationship()
    {
        var users = await CreateKnownUsersAsync(2);
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
        var dbContext = scope.ServiceProvider.GetRequiredService<FriendsDbContext>();
        Assert.Equal(1, await dbContext.FriendRequests.CountAsync(
            item => item.UserId1 == Min(userA, userB) &&
                    item.UserId2 == Max(userA, userB) &&
                    item.Status == FriendRequestStatus.Pending));
    }

    [Fact]
    public async Task Only_receiver_can_accept_and_concurrent_accept_creates_one_friendship()
    {
        var users = await CreateKnownUsersAsync(3);
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
        var dbContext = scope.ServiceProvider.GetRequiredService<FriendsDbContext>();
        Assert.Equal(1, await dbContext.Friendships.CountAsync(
            item => item.UserId1 == Min(userA, userB) && item.UserId2 == Max(userA, userB)));
        var outboxMessages = await dbContext.OutboxMessages.AsNoTracking().ToListAsync();
        Assert.Equal(1, outboxMessages.Count(
            item => item.Type == FriendRequestAcceptedIntegrationEvent.EventType &&
                    item.Payload.Contains(request.Id.ToString(), StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Receiver_can_decline_and_sender_can_cancel()
    {
        var users = await CreateKnownUsersAsync(3);
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
        var dbContext = scope.ServiceProvider.GetRequiredService<FriendsDbContext>();
        Assert.Equal(FriendRequestStatus.Declined,
            (await dbContext.FriendRequests.FindAsync(declinedRequest.Id))!.Status);
        Assert.Equal(FriendRequestStatus.Cancelled,
            (await dbContext.FriendRequests.FindAsync(cancelledRequest.Id))!.Status);
    }

    [Fact]
    public async Task Lists_and_status_reflect_pending_friendship_and_unfriend()
    {
        var users = await CreateKnownUsersAsync(2);
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
        Assert.Contains(outgoing!.Items, item => item.Id == request.Id);
        Assert.Contains(incoming!.Items, item => item.Id == request.Id);
        Assert.Equal("request_sent", pendingStatus!.Status);

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

        var unfriend = await clientA.DeleteAsync($"/api/friends/{userB}");
        Assert.Equal(HttpStatusCode.NoContent, unfriend.StatusCode);
        Assert.Equal("none", (await clientA.GetFromJsonAsync<RelationshipStatusResponse>(
            $"/api/friends/status/{userB}"))!.Status);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FriendsDbContext>();
        Assert.Contains(await dbContext.OutboxMessages.ToListAsync(),
            item => item.Type == FriendshipRemovedIntegrationEvent.EventType &&
                    item.Payload.Contains(userA.ToString()) &&
                    item.Payload.Contains(userB.ToString()));
    }

    [Fact]
    public async Task Block_removes_friendship_cancels_pending_and_prevents_both_directions()
    {
        var users = await CreateKnownUsersAsync(3);
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
        var dbContext = scope.ServiceProvider.GetRequiredService<FriendsDbContext>();
        Assert.False(await dbContext.Friendships.AnyAsync(
            item => item.UserId1 == Min(userA, userB) && item.UserId2 == Max(userA, userB)));
        Assert.Equal(FriendRequestStatus.Cancelled,
            (await dbContext.FriendRequests.FindAsync(pending.Id))!.Status);
        Assert.Contains(await dbContext.OutboxMessages.ToListAsync(),
            item => item.Type == UserBlockedIntegrationEvent.EventType &&
                    item.Payload.Contains(userB.ToString()));
    }

    [Fact]
    public async Task Unblock_does_not_restore_friendship()
    {
        var users = await CreateKnownUsersAsync(2);
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
        var dbContext = scope.ServiceProvider.GetRequiredService<FriendsDbContext>();
        Assert.Contains(await dbContext.OutboxMessages.ToListAsync(),
            item => item.Type == UserUnblockedIntegrationEvent.EventType &&
                    item.Payload.Contains(userA.ToString()) &&
                    item.Payload.Contains(userB.ToString()));
    }

    [Fact]
    public async Task Mutual_friends_returns_intersection_from_friends_database()
    {
        var users = await CreateKnownUsersAsync(4);
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
    public async Task Known_user_projection_is_idempotent()
    {
        var integrationEvent = new UserRegisteredIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            $"known_{Guid.NewGuid():N}"[..20],
            DateTimeOffset.UtcNow);

        var first = await HandleUserRegisteredAsync(integrationEvent);
        var duplicate = await HandleUserRegisteredAsync(integrationEvent);

        Assert.True(first);
        Assert.False(duplicate);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FriendsDbContext>();
        Assert.Equal(1, await dbContext.KnownUsers.CountAsync(
            user => user.UserId == integrationEvent.UserId));
        Assert.Equal(1, await dbContext.InboxMessages.CountAsync(
            message => message.EventId == integrationEvent.EventId));
    }

    [Fact]
    public async Task Pagination_rejects_excessive_limit()
    {
        var user = (await CreateKnownUsersAsync(1))[0];
        using var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/friends?limit=101");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
            await HandleUserRegisteredAsync(integrationEvent);
            userIds[index] = integrationEvent.UserId;
        }

        return userIds;
    }

    private async Task<bool> HandleUserRegisteredAsync(
        UserRegisteredIntegrationEvent integrationEvent)
    {
        using var scope = factory.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IUserRegisteredEventHandler>();
        return await handler.HandleAsync(integrationEvent);
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
}
