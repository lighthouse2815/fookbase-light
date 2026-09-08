using System.Text.Json;
using Fookbase.Contracts.Friends;
using Fookbase.Contracts.Identity;
using Fookbase.Contracts.Media;
using Fookbase.Contracts.Posts;
using Fookbase.Media.Infrastructure.IntegrationEvents;
using Fookbase.Posts.Infrastructure.Persistence;
using FriendsEventMessage = Fookbase.Friends.Application.Abstractions.IntegrationEventMessage;
using FriendsEventPublisher = Fookbase.Friends.Application.Abstractions.IIntegrationEventPublisher;
using FriendsRegistrationHandler = Fookbase.Friends.Application.Registrations.IUserRegisteredEventHandler;
using IdentityEventMessage = Fookbase.Identity.Application.Abstractions.IntegrationEventMessage;
using IdentityEventPublisher = Fookbase.Identity.Application.Abstractions.IIntegrationEventPublisher;
using MediaEventMessage = Fookbase.Media.Application.Abstractions.IntegrationEventMessage;
using MediaEventPublisher = Fookbase.Media.Application.Abstractions.IIntegrationEventPublisher;
using PostsEventMessage = Fookbase.Posts.Application.Abstractions.IntegrationEventMessage;
using PostsEventPublisher = Fookbase.Posts.Application.Abstractions.IIntegrationEventPublisher;
using UsersRegistrationHandler = Fookbase.Users.Application.Registrations.IUserRegisteredEventHandler;

namespace Fookbase.Api.IntegrationEvents;

internal sealed class InProcessIntegrationEventPublisher(
    UsersRegistrationHandler usersRegistrationHandler,
    FriendsRegistrationHandler friendsRegistrationHandler,
    EventProjectionStore postsProjectionStore,
    MediaProjectionStore mediaProjectionStore)
    : IdentityEventPublisher, FriendsEventPublisher, PostsEventPublisher, MediaEventPublisher
{
    Task IdentityEventPublisher.PublishAsync(
        IdentityEventMessage message,
        CancellationToken cancellationToken) =>
        DispatchAsync(message.Type, message.Payload, cancellationToken);

    Task FriendsEventPublisher.PublishAsync(
        FriendsEventMessage message,
        CancellationToken cancellationToken) =>
        DispatchAsync(message.Type, message.Payload, cancellationToken);

    Task PostsEventPublisher.PublishAsync(
        PostsEventMessage message,
        CancellationToken cancellationToken) =>
        DispatchAsync(message.Type, message.Payload, cancellationToken);

    Task MediaEventPublisher.PublishAsync(
        MediaEventMessage message,
        CancellationToken cancellationToken) =>
        DispatchAsync(message.Type, message.Payload, cancellationToken);

    private async Task DispatchAsync(
        string eventType,
        string payload,
        CancellationToken cancellationToken)
    {
        switch (eventType)
        {
            case UserRegisteredIntegrationEvent.EventType:
            {
                var message = Deserialize<UserRegisteredIntegrationEvent>(payload);
                await usersRegistrationHandler.HandleAsync(message, cancellationToken);
                await friendsRegistrationHandler.HandleAsync(message, cancellationToken);
                await postsProjectionStore.ProjectAsync(message, cancellationToken);
                await mediaProjectionStore.ProjectAsync(message, cancellationToken);
                break;
            }
            case FriendRequestAcceptedIntegrationEvent.EventType:
                await postsProjectionStore.ProjectAsync(
                    Deserialize<FriendRequestAcceptedIntegrationEvent>(payload), cancellationToken);
                break;
            case FriendshipRemovedIntegrationEvent.EventType:
                await postsProjectionStore.ProjectAsync(
                    Deserialize<FriendshipRemovedIntegrationEvent>(payload), cancellationToken);
                break;
            case UserBlockedIntegrationEvent.EventType:
                await postsProjectionStore.ProjectAsync(
                    Deserialize<UserBlockedIntegrationEvent>(payload), cancellationToken);
                break;
            case UserUnblockedIntegrationEvent.EventType:
                await postsProjectionStore.ProjectAsync(
                    Deserialize<UserUnblockedIntegrationEvent>(payload), cancellationToken);
                break;
            case MediaReadyIntegrationEvent.EventType:
                await postsProjectionStore.ProjectAsync(
                    Deserialize<MediaReadyIntegrationEvent>(payload), cancellationToken);
                break;
            case MediaDeletedIntegrationEvent.EventType:
                await postsProjectionStore.ProjectAsync(
                    Deserialize<MediaDeletedIntegrationEvent>(payload), cancellationToken);
                break;
            case PostMediaAttachedIntegrationEvent.EventType:
                await mediaProjectionStore.ProjectAsync(
                    Deserialize<PostMediaAttachedIntegrationEvent>(payload), cancellationToken);
                break;
            case PostMediaDetachedIntegrationEvent.EventType:
                await mediaProjectionStore.ProjectAsync(
                    Deserialize<PostMediaDetachedIntegrationEvent>(payload), cancellationToken);
                break;
            case FriendRequestSentIntegrationEvent.EventType:
            case PostCreatedIntegrationEvent.EventType:
            case PostUpdatedIntegrationEvent.EventType:
            case PostDeletedIntegrationEvent.EventType:
            case CommentCreatedIntegrationEvent.EventType:
            case PostReactionChangedIntegrationEvent.EventType:
                // These events are retained in the outbox for audit, but no module currently consumes them.
                break;
            default:
                throw new JsonException($"Unsupported integration event type '{eventType}'.");
        }
    }

    private static T Deserialize<T>(string payload) =>
        JsonSerializer.Deserialize<T>(payload)
        ?? throw new JsonException("Integration event payload was empty.");
}
