using System.Text.Json;
using Fookbase.Api.Shared.Contracts.Identity;
using Fookbase.Api.Shared.Contracts.Media;
using Fookbase.Api.Shared.Contracts.Posts;
using Fookbase.Api.Modules.Media.Repositories;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Posts.Repositories;
using IdentityEventMessage = Fookbase.Api.Modules.Identity.Messaging.IntegrationEventMessage;
using IdentityEventPublisher = Fookbase.Api.Modules.Identity.Messaging.IIntegrationEventPublisher;
using MediaEventMessage = Fookbase.Api.Modules.Media.Services.IntegrationEventMessage;
using MediaEventPublisher = Fookbase.Api.Modules.Media.Services.IIntegrationEventPublisher;
using PostsEventMessage = Fookbase.Api.Modules.Posts.Services.IntegrationEventMessage;
using PostsEventPublisher = Fookbase.Api.Modules.Posts.Services.IIntegrationEventPublisher;
using UsersRegistrationHandler = Fookbase.Api.Modules.Users.Messaging.UserRegisteredEventHandler;

namespace Fookbase.Api.Shared.IntegrationEvents;

internal sealed class InProcessIntegrationEventPublisher(
    UsersRegistrationHandler usersRegistrationHandler,
    EventProjectionStore postsProjectionStore,
    MediaProjectionStore mediaProjectionStore)
    : IdentityEventPublisher, PostsEventPublisher, MediaEventPublisher
{
    Task IdentityEventPublisher.PublishAsync(
        IdentityEventMessage message,
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
                await postsProjectionStore.ProjectAsync(message, cancellationToken);
                await mediaProjectionStore.ProjectAsync(message, cancellationToken);
                break;
            }
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
