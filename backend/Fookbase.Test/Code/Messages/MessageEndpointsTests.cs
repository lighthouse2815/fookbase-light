using Fookbase.Api.Modules.Media.Domain.Enums;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Fookbase.Api.Modules.Messages.DTOs.Responses;
using Fookbase.Api.Modules.Friends.Entities;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Messages.Domain.Enums;
using Fookbase.Api.Modules.Messages.Entities;
using Fookbase.Api.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Messages.Api.IntegrationTests;

public sealed class MessageEndpointsTests(MessagesApiFactory factory)
    : IClassFixture<MessagesApiFactory>
{
    [Theory]
    [InlineData("POST", "conversations/{id}")]
    [InlineData("POST", "conversations/direct")]
    [InlineData("POST", "conversations/group")]
    [InlineData("GET", "conversations")]
    [InlineData("GET", "conversations/{id}")]
    [InlineData("PATCH", "conversations/{id}")]
    [InlineData("GET", "conversations/{id}/messages")]
    [InlineData("POST", "conversations/{id}/messages")]
    [InlineData("GET", "conversations/{id}/search")]
    [InlineData("POST", "conversations/{id}/read")]
    [InlineData("POST", "conversations/{id}/participants")]
    [InlineData("DELETE", "conversations/{id}/participants/{id}")]
    [InlineData("PATCH", "conversations/{id}/participants/{id}/role")]
    [InlineData("POST", "conversations/{id}/leave")]
    [InlineData("POST", "conversations/{id}/transfer-ownership")]
    [InlineData("POST", "{id}/reactions")]
    [InlineData("DELETE", "{id}/reactions")]
    [InlineData("PATCH", "{id}")]
    [InlineData("DELETE", "{id}")]
    [InlineData("GET", "media/{id}/read-url")]
    [InlineData("GET", "notifications")]
    public async Task Message_routes_require_authentication(string method, string route)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method),
            "/api/messages/" + route.Replace("{id}", Guid.NewGuid().ToString()))
        {
            Content = JsonContent.Create(new { })
        };

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Zola_push_token_registration_is_authenticated_and_can_be_removed()
    {
        var userId = (await CreateUsersAsync(1))[0];
        const string token = "ExpoPushToken[zola_push_device_123]";
        using var client = CreateAuthenticatedClient(userId);

        var invalid = await client.PostAsJsonAsync("/api/notifications/push-tokens/zola", new { token = "not-an-expo-token" });
        var registered = await client.PostAsJsonAsync("/api/notifications/push-tokens/zola", new { token });

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var error = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync());
        Assert.Equal("validation_failed", error.RootElement.GetProperty("code").GetString());
        Assert.True(error.RootElement.GetProperty("errors").TryGetProperty("Token", out _));
        Assert.Equal(HttpStatusCode.NoContent, registered.StatusCode);
        foreach (var invalidToken in new string?[]
                 { null, "", "   ", "invalid", "ExpoPushToken[]", "ExpoPushToken[a b]", "ExpoPushToken[a]\n",
                     "ExpoPushToken[" + new string('x', 241) + "]" })
        {
            foreach (var method in new[] { HttpMethod.Post, HttpMethod.Delete })
            {
                using var invalidRequest = new HttpRequestMessage(method, "/api/notifications/push-tokens/zola")
                {
                    Content = JsonContent.Create(new { token = invalidToken })
                };
                using var rejected = await client.SendAsync(invalidRequest);
                Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
                using var validation = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
                Assert.Equal("validation_failed", validation.RootElement.GetProperty("code").GetString());
                Assert.True(validation.RootElement.GetProperty("errors").TryGetProperty("Token", out _));
            }
        }
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            Assert.Contains(await dbContext.PushDevices.ToListAsync(), device =>
                device.UserId == userId && device.ExpoPushToken == token && device.DisabledAtUtc is null);
        }

        var removed = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/notifications/push-tokens/zola")
        {
            Content = JsonContent.Create(new { token })
        });
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        using var verificationScope = factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.NotNull((await verificationDb.PushDevices.SingleAsync(device => device.ExpoPushToken == token)).DisabledAtUtc);
    }

    [Theory]
    [InlineData("Exponent", "letters_123-ABC")]
    [InlineData("Expo", "letters_123-ABC")]
    [InlineData("Expo", null)]
    public async Task Valid_push_tokens_can_be_registered_again_and_only_the_owner_can_disable_them(
        string prefix, string? payload)
    {
        var token = $"{prefix}PushToken[{payload ?? new string('a', 240)}]";
        var users = await CreateUsersAsync(2);
        using var owner = CreateAuthenticatedClient(users[0]);
        using var other = CreateAuthenticatedClient(users[1]);
        using var missingRemoval = await owner.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/notifications/push-tokens/zola")
        {
            Content = JsonContent.Create(new { token })
        });
        Assert.Equal(HttpStatusCode.NoContent, missingRemoval.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.PostAsJsonAsync("/api/notifications/push-tokens/zola", new { token })).StatusCode);
        using var otherRemoval = await other.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/notifications/push-tokens/zola")
        {
            Content = JsonContent.Create(new { token })
        });
        Assert.Equal(HttpStatusCode.NoContent, otherRemoval.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            Assert.Null((await db.PushDevices.SingleAsync(device => device.ExpoPushToken == token)).DisabledAtUtc);
        }

        using var removal = await owner.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/notifications/push-tokens/zola")
        {
            Content = JsonContent.Create(new { token })
        });
        Assert.Equal(HttpStatusCode.NoContent, removal.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.PostAsJsonAsync("/api/notifications/push-tokens/zola", new { token })).StatusCode);
        using var verificationScope = factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var registered = await verificationDb.PushDevices.SingleAsync(device => device.ExpoPushToken == token);
        Assert.Equal(users[0], registered.UserId);
        Assert.Null(registered.DisabledAtUtc);
    }

    [Fact]
    public async Task Direct_conversation_is_reused_and_messages_require_an_explicit_read_marker()
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
        var reversed = await recipient.PostAsJsonAsync("/api/messages/conversations/direct", new { userId = senderUserId });
        Assert.Equal(HttpStatusCode.OK, reversed.StatusCode);
        Assert.Equal(conversation.Id, (await reversed.Content.ReadFromJsonAsync<ConversationResponse>())!.Id);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            var participants = await dbContext.ConversationParticipants
                .Where(participant => participant.ConversationId == conversation.Id)
                .ToListAsync();
            Assert.Equal(2, participants.Count);
            Assert.Contains(participants, participant => participant.UserId == senderUserId);
            Assert.Contains(participants, participant => participant.UserId == recipientUserId);
        }

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

        var history = await recipient.GetFromJsonAsync<MessageHistoryResponse>(
            $"/api/messages/conversations/{conversation.Id}/messages");
        Assert.NotNull(history);
        var message = Assert.Single(history!.Items);
        Assert.Equal(senderUserId, message.SenderUserId);
        Assert.Equal("Hello from the sender", message.Content);
        Assert.Null(message.ReadAtUtc);
        var notificationsAfterHistory = await recipient.GetFromJsonAsync<PagedResponse<IncomingMessageResponse>>(
            "/api/messages/notifications");
        Assert.NotNull(notificationsAfterHistory);
        Assert.Single(notificationsAfterHistory!.Items);

        var markRead = await recipient.PostAsJsonAsync(
            $"/api/messages/conversations/{conversation.Id}/read",
            new { lastReadMessageId = message.Id });
        Assert.Equal(HttpStatusCode.NoContent, markRead.StatusCode);

        var readHistory = await recipient.GetFromJsonAsync<MessageHistoryResponse>(
            $"/api/messages/conversations/{conversation.Id}/messages");
        Assert.NotNull(readHistory);
        Assert.NotNull(Assert.Single(readHistory!.Items).ReadAtUtc);
        var remainingNotifications = await recipient.GetFromJsonAsync<PagedResponse<IncomingMessageResponse>>(
            "/api/messages/notifications");
        Assert.NotNull(remainingNotifications);
        Assert.Empty(remainingNotifications!.Items);
    }

    [Fact]
    public async Task Concurrent_direct_creation_returns_one_conversation()
    {
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        await BecomeFriendsAsync(firstUserId, secondUserId);
        using var first = CreateAuthenticatedClient(firstUserId);

        var responses = await Task.WhenAll(
            first.PostAsync($"/api/messages/conversations/{secondUserId}", null),
            first.PostAsync($"/api/messages/conversations/{secondUserId}", null));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var conversations = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<ConversationResponse>()));
        Assert.All(conversations, conversation => Assert.NotNull(conversation));
        Assert.Single(conversations.Select(conversation => conversation!.Id).Distinct());
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Equal(1, await dbContext.Conversations.CountAsync(conversation =>
            conversation.Type == ConversationType.DIRECT &&
            dbContext.ConversationParticipants.Count(participant => participant.ConversationId == conversation.Id && participant.LeftAtUtc == null) == 2 &&
            dbContext.ConversationParticipants.Any(participant => participant.ConversationId == conversation.Id && participant.UserId == firstUserId) &&
            dbContext.ConversationParticipants.Any(participant => participant.ConversationId == conversation.Id && participant.UserId == secondUserId)));
    }

    [Fact]
    public async Task Message_history_returns_the_newest_page_then_stable_older_cursor_pages()
    {
        var senderUserId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        await BecomeFriendsAsync(senderUserId, recipientUserId);
        using var sender = CreateAuthenticatedClient(senderUserId);
        using var recipient = CreateAuthenticatedClient(recipientUserId);

        var created = await sender.PostAsync($"/api/messages/conversations/{recipientUserId}", null);
        var conversation = await created.Content.ReadFromJsonAsync<ConversationResponse>();
        Assert.NotNull(conversation);
        foreach (var content in new[] { "oldest", "middle", "newest" })
        {
            var sent = await sender.PostAsJsonAsync(
                $"/api/messages/conversations/{conversation!.Id}/messages",
                new { content });
            Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        }

        var newestPage = await recipient.GetFromJsonAsync<MessageHistoryResponse>(
            $"/api/messages/conversations/{conversation!.Id}/messages?limit=2");
        Assert.NotNull(newestPage);
        Assert.Equal(new[] { "middle", "newest" }, newestPage!.Items.Select(message => message.Content));
        Assert.True(newestPage.HasMore);
        Assert.False(string.IsNullOrWhiteSpace(newestPage.NextCursor));
        Assert.All(newestPage.Items, message => Assert.Null(message.ReadAtUtc));

        var olderPage = await recipient.GetFromJsonAsync<MessageHistoryResponse>(
            $"/api/messages/conversations/{conversation.Id}/messages?limit=2&before={Uri.EscapeDataString(newestPage.NextCursor!)}");
        Assert.NotNull(olderPage);
        Assert.Equal(new[] { "oldest" }, olderPage!.Items.Select(message => message.Content));
        Assert.False(olderPage.HasMore);
        Assert.Null(olderPage.NextCursor);

        var notifications = await recipient.GetFromJsonAsync<PagedResponse<IncomingMessageResponse>>(
            "/api/messages/notifications");
        Assert.NotNull(notifications);
        Assert.Equal(3, notifications!.Items.Count);
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
        using var readError = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        Assert.Equal("conversation_access_denied", readError.RootElement.GetProperty("code").GetString());
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

    [Fact]
    public async Task Reply_reaction_edit_and_unsend_use_the_same_conversation_and_hide_deleted_content()
    {
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        await BecomeFriendsAsync(firstUserId, secondUserId);
        using var first = CreateAuthenticatedClient(firstUserId);
        using var second = CreateAuthenticatedClient(secondUserId);

        var conversation = await (await first.PostAsync($"/api/messages/conversations/{secondUserId}", null))
            .Content.ReadFromJsonAsync<ConversationResponse>();
        Assert.NotNull(conversation);
        var original = await (await first.PostAsJsonAsync(
            $"/api/messages/conversations/{conversation!.Id}/messages", new { content = "original" }))
            .Content.ReadFromJsonAsync<MessageResponse>();
        Assert.NotNull(original);

        var replyResponse = await second.PostAsJsonAsync(
            $"/api/messages/conversations/{conversation.Id}/messages",
            new { content = "reply", replyToMessageId = original!.Id });
        Assert.Equal(HttpStatusCode.Created, replyResponse.StatusCode);
        var reply = await replyResponse.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.NotNull(reply);
        Assert.Equal(original.Id, reply!.ReplyToMessageId);
        Assert.Equal("original", reply.ReplyTo!.Content);

        Assert.Equal(HttpStatusCode.OK, (await first.PostAsJsonAsync(
            $"/api/messages/{reply.Id}/reactions", new { type = "love" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await first.PostAsJsonAsync(
            $"/api/messages/{reply.Id}/reactions", new { type = "haha" })).StatusCode);
        var reactedHistory = await second.GetFromJsonAsync<MessageHistoryResponse>(
            $"/api/messages/conversations/{conversation.Id}/messages");
        var reaction = Assert.Single(Assert.Single(reactedHistory!.Items, message => message.Id == reply.Id).Reactions!);
        Assert.Equal(firstUserId, reaction.UserId);
        Assert.Equal("haha", reaction.Type);
        Assert.Equal(HttpStatusCode.NoContent, (await first.DeleteAsync($"/api/messages/{reply.Id}/reactions")).StatusCode);
        var foreignEdit = await first.PatchAsJsonAsync($"/api/messages/{reply.Id}", new { content = "forbidden" });
        Assert.Equal(HttpStatusCode.Forbidden, foreignEdit.StatusCode);

        var edited = await second.PatchAsJsonAsync($"/api/messages/{reply.Id}", new { content = "edited reply" });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal("edited reply", (await edited.Content.ReadFromJsonAsync<MessageResponse>())!.Content);
        Assert.Equal(HttpStatusCode.NoContent, (await second.DeleteAsync($"/api/messages/{reply.Id}")).StatusCode);

        var history = await first.GetFromJsonAsync<MessageHistoryResponse>(
            $"/api/messages/conversations/{conversation.Id}/messages");
        var deleted = Assert.Single(history!.Items, message => message.Id == reply.Id);
        Assert.Null(deleted.Content);
        Assert.NotNull(deleted.DeletedAtUtc);
        Assert.Empty(deleted.Attachments!);
    }

    [Fact]
    public async Task Group_conversation_creates_owner_and_members_and_excludes_removed_member_from_access()
    {
        var users = await CreateUsersAsync(3);
        using var owner = CreateAuthenticatedClient(users[0]);
        using var member = CreateAuthenticatedClient(users[1]);
        using var removed = CreateAuthenticatedClient(users[2]);

        var created = await owner.PostAsJsonAsync("/api/messages/conversations/group", new
        {
            title = "Test group",
            participantUserIds = new[] { users[1], users[2] }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var conversation = await created.Content.ReadFromJsonAsync<ConversationResponse>();
        Assert.NotNull(conversation);
        Assert.Equal("group", conversation!.Type);
        Assert.Equal(3, conversation.Participants!.Count);
        Assert.Equal("owner", conversation.Participants.Single(participant => participant.UserId == users[0]).Role);

        Assert.Equal(HttpStatusCode.Created, (await member.PostAsJsonAsync(
            $"/api/messages/conversations/{conversation.Id}/messages", new { content = "hello group" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync(
            $"/api/messages/conversations/{conversation.Id}/participants/{users[2]}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await removed.GetAsync(
            $"/api/messages/conversations/{conversation.Id}/messages")).StatusCode);
    }

    [Fact]
    public async Task Group_block_filter_hides_reply_preview_and_rejects_reactions_by_message_id()
    {
        var users = await CreateUsersAsync(3);
        using var first = CreateAuthenticatedClient(users[0]);
        using var blocked = CreateAuthenticatedClient(users[1]);
        using var third = CreateAuthenticatedClient(users[2]);
        var conversation = await (await first.PostAsJsonAsync("/api/messages/conversations/group", new
        {
            title = "Block audit",
            participantUserIds = new[] { users[1], users[2] }
        })).Content.ReadFromJsonAsync<ConversationResponse>();
        Assert.NotNull(conversation);
        await BlockAsync(users[0], users[1]);

        var blockedMessage = await (await blocked.PostAsJsonAsync(
            $"/api/messages/conversations/{conversation!.Id}/messages", new { content = "blocked secret" }))
            .Content.ReadFromJsonAsync<MessageResponse>();
        Assert.NotNull(blockedMessage);
        var attachmentMediaId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            var media = new MediaAsset(attachmentMediaId, users[1], MediaType.IMAGE,
                $"tests/{attachmentMediaId}", "blocked.png", "image/png", 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1));
            media.MarkReady(1, DateTimeOffset.UtcNow);
            dbContext.MediaAssets.Add(media);
            dbContext.MessageAttachments.Add(new MessageAttachment(blockedMessage!.Id, attachmentMediaId, 0));
            await dbContext.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Created, (await third.PostAsJsonAsync(
            $"/api/messages/conversations/{conversation.Id}/messages", new { content = "visible reply", replyToMessageId = blockedMessage!.Id })).StatusCode);

        var history = await first.GetFromJsonAsync<MessageHistoryResponse>(
            $"/api/messages/conversations/{conversation.Id}/messages");
        var visibleReply = Assert.Single(history!.Items);
        Assert.Equal("visible reply", visibleReply.Content);
        Assert.Null(visibleReply.ReplyTo);
        var search = await first.GetFromJsonAsync<IReadOnlyList<MessageResponse>>(
            $"/api/messages/conversations/{conversation.Id}/search?q=blocked");
        Assert.Empty(search!);
        Assert.Equal(HttpStatusCode.Forbidden, (await first.PostAsJsonAsync(
            $"/api/messages/{blockedMessage.Id}/reactions", new { type = "like" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await first.GetAsync(
            $"/api/messages/media/{attachmentMediaId}/read-url")).StatusCode);
    }

    public static IEnumerable<object[]> InvalidMessageRequests()
    {
        var userId = Guid.NewGuid();
        yield return ["POST", "conversations/direct", new { userId = Guid.Empty }, "UserId"];
        yield return ["POST", "conversations/{id}/transfer-ownership", new { userId = Guid.Empty }, "UserId"];
        foreach (var title in new string?[] { null, "   ", new('t', 121) })
            yield return ["POST", "conversations/group", new { title, participantUserIds = new[] { userId } }, "Title"];
        foreach (var ids in new Guid[]?[] { null, [], [Guid.Empty], [userId, userId], Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToArray() })
            yield return ["POST", "conversations/group", new { title = "Group", participantUserIds = ids }, "ParticipantUserIds"];
        foreach (var ids in new Guid[]?[] { null, [], [Guid.Empty], [userId, userId], Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToArray() })
            yield return ["POST", "conversations/{id}/participants", new { userIds = ids }, "UserIds"];
        foreach (var content in new string?[] { null, "   ", new('m', 5001) })
        {
            yield return ["POST", "conversations/{id}/messages", new { content }, "Content"];
            yield return ["PATCH", "{id}", new { content }, "Content"];
        }
        foreach (var ids in new Guid[][] { [Guid.Empty], [userId, userId], Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToArray() })
            yield return ["POST", "conversations/{id}/messages", new { content = "Photo", mediaIds = ids }, "MediaIds"];
        foreach (var type in new string?[] { null, " ", "unknown", "999" })
            yield return ["POST", "{id}/reactions", new { type }, "Type"];
        foreach (var role in new string?[] { null, " ", "owner", "unknown", "999" })
            yield return ["PATCH", "conversations/{id}/participants/{id}/role", new { role }, "Role"];
        foreach (var title in new[] { " ", new string('t', 121) })
            yield return ["PATCH", "conversations/{id}", new { title }, "Title"];
        yield return ["PATCH", "conversations/{id}", new { nickname = new string('n', 81) }, "Nickname"];
        foreach (var query in new[] { "", "?q=%20", "?q=" + new string('q', 201) })
            yield return ["GET", "conversations/{id}/search" + query, null!, "q"];
        foreach (var route in new[] { "conversations", "conversations/{id}/messages" })
        {
            foreach (var limit in new[] { "0", "101", "invalid" })
                yield return ["GET", route + "?limit=" + limit, null!, "Limit"];
            foreach (var before in new[] { "invalid", "bnVsbA", "W10", new string('x', 257) })
                yield return ["GET", route + "?before=" + before, null!, "Before"];
        }
        yield return ["GET", "notifications?offset=-1", null!, "Offset"];
        foreach (var limit in new[] { "0", "101" })
            yield return ["GET", "notifications?limit=" + limit, null!, "Limit"];
    }

    [Theory]
    [MemberData(nameof(InvalidMessageRequests))]
    public async Task Invalid_message_requests_return_field_validation_errors(
        string method, string route, object? payload, string field)
    {
        using var client = CreateAuthenticatedClient(Guid.NewGuid());
        using var request = new HttpRequestMessage(new HttpMethod(method),
            "/api/messages/" + route.Replace("{id}", Guid.NewGuid().ToString()))
        {
            Content = payload is null ? null : JsonContent.Create(payload)
        };
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("validation_failed", problem.RootElement.GetProperty("code").GetString());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Valid_message_boundaries_are_normalized_and_keep_business_checks()
    {
        var users = await CreateUsersAsync(2);
        using var owner = CreateAuthenticatedClient(users[0]);
        using var participant = CreateAuthenticatedClient(users[1]);
        var title = new string('t', 120);
        using var created = await owner.PostAsJsonAsync("/api/messages/conversations/group", new
        {
            title = "  " + title + "  ", participantUserIds = new[] { users[1] }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var conversation = (await created.Content.ReadFromJsonAsync<ConversationResponse>())!;
        Assert.Equal(title, conversation.Title);
        Assert.Equal($"/api/messages/conversations/{conversation.Id}", created.Headers.Location!.OriginalString);
        var content = new string('m', 5000);
        using var sent = await owner.PostAsJsonAsync($"/api/messages/conversations/{conversation.Id}/messages",
            new { content = "  " + content + "  " });
        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        var message = (await sent.Content.ReadFromJsonAsync<MessageResponse>())!;
        Assert.Equal(content, message.Content);
        Assert.Equal($"/api/messages/conversations/{conversation.Id}/messages/{message.Id}", sent.Headers.Location!.OriginalString);
        using var edited = await owner.PatchAsJsonAsync($"/api/messages/{message.Id}", new { content = " " + content + " " });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal(content, (await edited.Content.ReadFromJsonAsync<MessageResponse>())!.Content);
        using var reacted = await participant.PostAsJsonAsync($"/api/messages/{message.Id}/reactions", new { type = "LoVe" });
        Assert.Equal(HttpStatusCode.OK, reacted.StatusCode);
        Assert.Equal("love", (await reacted.Content.ReadFromJsonAsync<MessageReactionResponse>())!.Type);
        using var role = await owner.PatchAsJsonAsync($"/api/messages/conversations/{conversation.Id}/participants/{users[1]}/role", new { role = "AdMiN" });
        Assert.Equal(HttpStatusCode.NoContent, role.StatusCode);
        using var update = await owner.PatchAsJsonAsync($"/api/messages/conversations/{conversation.Id}", new { nickname = " " + new string('n', 80) + " " });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        using var history = await owner.GetAsync($"/api/messages/conversations/{conversation.Id}/messages?limit=100&before=%20");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        using var notifications = await owner.GetAsync("/api/messages/notifications?offset=0&limit=100");
        Assert.Equal(HttpStatusCode.OK, notifications.StatusCode);
        using var selfParticipant = await owner.PostAsJsonAsync("/api/messages/conversations/group", new { title = "Self", participantUserIds = new[] { users[0] } });
        Assert.Equal(HttpStatusCode.BadRequest, selfParticipant.StatusCode);
        using var selfProblem = JsonDocument.Parse(await selfParticipant.Content.ReadAsStringAsync());
        Assert.Equal("invalid_group_participants", selfProblem.RootElement.GetProperty("code").GetString());
        using var missingParticipant = await owner.PostAsJsonAsync("/api/messages/conversations/group", new { title = "Missing", participantUserIds = new[] { Guid.NewGuid() } });
        Assert.Equal(HttpStatusCode.NotFound, missingParticipant.StatusCode);
        using var missingProblem = JsonDocument.Parse(await missingParticipant.Content.ReadAsStringAsync());
        Assert.Equal("group_participant_not_found", missingProblem.RootElement.GetProperty("code").GetString());

        var now = DateTimeOffset.UtcNow;
        var mediaIds = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            foreach (var id in mediaIds)
            {
                var media = new MediaAsset(id, users[0], MediaType.IMAGE,
                    $"tests/{id}", "photo.png", "image/png", 1, now, now.AddMinutes(1));
                media.MarkReady(1, now);
                db.MediaAssets.Add(media);
            }
            await db.SaveChangesAsync();
        }
        using var mediaSent = await owner.PostAsJsonAsync($"/api/messages/conversations/{conversation.Id}/messages", new { content = (string?)null, mediaIds });
        Assert.Equal(HttpStatusCode.Created, mediaSent.StatusCode);
        var mediaMessage = (await mediaSent.Content.ReadFromJsonAsync<MessageResponse>())!;
        Assert.Null(mediaMessage.Content);
        Assert.Equal("media", mediaMessage.Type);
        Assert.Equal(mediaIds, mediaMessage.Attachments!.Select(attachment => attachment.MediaId));

        using var secondCreated = await owner.PostAsJsonAsync("/api/messages/conversations/group",
            new { title = "Second conversation", participantUserIds = new[] { users[1] } });
        Assert.Equal(HttpStatusCode.Created, secondCreated.StatusCode);
        var firstPage = (await owner.GetFromJsonAsync<PagedResponse<ConversationResponse>>("/api/messages/conversations?limit=1"))!;
        Assert.Single(firstPage.Items);
        Assert.NotNull(firstPage.NextCursor);
        var secondPage = (await owner.GetFromJsonAsync<PagedResponse<ConversationResponse>>(
            "/api/messages/conversations?limit=1&before=" + Uri.EscapeDataString(firstPage.NextCursor)))!;
        Assert.Single(secondPage.Items);
        Assert.NotEqual(firstPage.Items[0].Id, secondPage.Items[0].Id);
        Assert.Null(secondPage.NextCursor);
    }

    [Fact]
    public async Task Group_size_limit_counts_existing_members_after_request_validation()
    {
        var users = await CreateUsersAsync(51);
        using var owner = CreateAuthenticatedClient(users[0]);
        using var created = await owner.PostAsJsonAsync("/api/messages/conversations/group", new
        {
            title = "Full group", participantUserIds = users.Skip(1).Take(49).ToArray()
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var conversation = (await created.Content.ReadFromJsonAsync<ConversationResponse>())!;
        Assert.Equal(50, conversation.Participants!.Count);

        using var added = await owner.PostAsJsonAsync($"/api/messages/conversations/{conversation.Id}/participants",
            new { userIds = new[] { users[50] } });
        Assert.Equal(HttpStatusCode.BadRequest, added.StatusCode);
        using var problem = JsonDocument.Parse(await added.Content.ReadAsStringAsync());
        Assert.Equal("group_size_limit", problem.RootElement.GetProperty("code").GetString());
        var unchanged = (await owner.GetFromJsonAsync<ConversationResponse>($"/api/messages/conversations/{conversation.Id}"))!;
        Assert.Equal(50, unchanged.Participants!.Count);
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
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var ids = new[] { firstUserId, secondUserId };
        var existingIds = await dbContext.Users.Where(user => ids.Contains(user.Id)).Select(user => user.Id).ToArrayAsync();
        var now = DateTimeOffset.UtcNow;
        var missingUsers = ids.Except(existingIds).Select(id => new User(
            id, $"messenger-{id:N}@example.com", $"messenger_{id:N}"[..32], now)).ToArray();
        dbContext.Users.AddRange(missingUsers);
        dbContext.UserProfiles.AddRange(missingUsers.Select(user => new UserProfile(user.Id, user.UserName!, now)));
        dbContext.Friendships.Add(new Friendship(
            firstUserId,
            secondUserId,
            DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    private async Task BlockAsync(Guid blockerUserId, Guid blockedUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        dbContext.BlockedUsers.Add(new BlockedUser(blockerUserId, blockedUserId, DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    private async Task<Guid[]> CreateUsersAsync(int count)
    {
        var now = DateTimeOffset.UtcNow;
        var users = Enumerable.Range(0, count)
            .Select(index => new User(
                Guid.NewGuid(),
                $"messenger-{Guid.NewGuid():N}@example.com",
                $"messenger_{Guid.NewGuid():N}"[..32],
                now.AddTicks(index)))
            .ToArray();
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        dbContext.Users.AddRange(users);
        dbContext.UserProfiles.AddRange(users.Select(user => new UserProfile(user.Id, user.UserName!, now)));
        await dbContext.SaveChangesAsync();
        return users.Select(user => user.Id).ToArray();
    }
}
