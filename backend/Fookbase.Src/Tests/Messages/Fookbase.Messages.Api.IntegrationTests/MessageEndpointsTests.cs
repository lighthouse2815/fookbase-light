using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Fookbase.Api.Modules.Messages.DTOs.Responses;
using Fookbase.Api.Modules.Friends.Data;
using Fookbase.Api.Modules.Friends.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Messages.Api.IntegrationTests;

public sealed class MessageEndpointsTests(MessagesApiFactory factory)
    : IClassFixture<MessagesApiFactory>
{
    [Fact]
    public async Task Missing_jwt_returns_unauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/messages/conversations");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Direct_conversation_is_reused_and_messages_are_read_by_recipient()
    {
        var senderUserId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        await BecomeFriendsAsync(senderUserId, recipientUserId);
        using var sender = CreateAuthenticatedClient(senderUserId);
        using var recipient = CreateAuthenticatedClient(recipientUserId);

        var created = await sender.PostAsync($"/api/messages/conversations/{recipientUserId}", null);
        var reused = await sender.PostAsync($"/api/messages/conversations/{recipientUserId}", null);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reused.StatusCode);
        var conversation = await created.Content.ReadFromJsonAsync<ConversationResponse>();
        var repeatedConversation = await reused.Content.ReadFromJsonAsync<ConversationResponse>();
        Assert.NotNull(conversation);
        Assert.Equal(conversation!.Id, repeatedConversation!.Id);

        var sent = await sender.PostAsJsonAsync(
            $"/api/messages/conversations/{conversation.Id}/messages",
            new { content = "Hello from the sender" });
        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);

        var conversations = await recipient.GetFromJsonAsync<PagedResponse<ConversationResponse>>(
            "/api/messages/conversations");
        Assert.NotNull(conversations);
        Assert.Contains(conversations!.Items, item =>
            item.Id == conversation.Id && item.UnreadCount == 1 && item.LastMessage!.Content == "Hello from the sender");
        var notifications = await recipient.GetFromJsonAsync<PagedResponse<IncomingMessageResponse>>(
            "/api/messages/notifications");
        Assert.NotNull(notifications);
        Assert.Contains(notifications!.Items, item =>
            item.Conversation.Id == conversation.Id && item.Message.Content == "Hello from the sender");

        var history = await recipient.GetFromJsonAsync<PagedResponse<MessageResponse>>(
            $"/api/messages/conversations/{conversation.Id}/messages");
        Assert.NotNull(history);
        var message = Assert.Single(history!.Items);
        Assert.Equal(senderUserId, message.SenderUserId);
        Assert.Equal("Hello from the sender", message.Content);
        Assert.NotNull(message.ReadAtUtc);
        var remainingNotifications = await recipient.GetFromJsonAsync<PagedResponse<IncomingMessageResponse>>(
            "/api/messages/notifications");
        Assert.NotNull(remainingNotifications);
        Assert.Empty(remainingNotifications!.Items);
    }

    [Fact]
    public async Task Outsider_cannot_read_or_send_messages()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        await BecomeFriendsAsync(userA, userB);
        using var owner = CreateAuthenticatedClient(userA);
        using var outsider = CreateAuthenticatedClient(Guid.NewGuid());

        var created = await owner.PostAsync($"/api/messages/conversations/{userB}", null);
        var conversation = await created.Content.ReadFromJsonAsync<ConversationResponse>();
        Assert.NotNull(conversation);

        var read = await outsider.GetAsync($"/api/messages/conversations/{conversation!.Id}/messages");
        var send = await outsider.PostAsJsonAsync(
            $"/api/messages/conversations/{conversation.Id}/messages",
            new { content = "Not allowed" });

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, send.StatusCode);
    }

    [Fact]
    public async Task Non_friends_or_blocked_users_cannot_start_a_conversation()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(userA);

        var nonFriend = await client.PostAsync($"/api/messages/conversations/{userB}", null);
        Assert.Equal(HttpStatusCode.Forbidden, nonFriend.StatusCode);

        await BecomeFriendsAsync(userA, userB);
        await BlockAsync(userB, userA);
        var blocked = await client.PostAsync($"/api/messages/conversations/{userB}", null);

        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
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

    private async Task BecomeFriendsAsync(Guid firstUserId, Guid secondUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FriendsDbContext>();
        dbContext.Friendships.Add(Friendship.Create(
            Guid.NewGuid(),
            firstUserId,
            secondUserId,
            DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    private async Task BlockAsync(Guid blockerUserId, Guid blockedUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FriendsDbContext>();
        dbContext.BlockedUsers.Add(BlockedUser.Create(blockerUserId, blockedUserId, DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
    }
}
