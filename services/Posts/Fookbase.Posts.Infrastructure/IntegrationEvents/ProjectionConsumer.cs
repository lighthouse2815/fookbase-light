using System.Text.Json;
using Fookbase.Contracts.Friends;
using Fookbase.Contracts.Identity;
using Fookbase.Posts.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Fookbase.Posts.Infrastructure.IntegrationEvents;

internal sealed class ProjectionConsumer(
    IServiceScopeFactory scopeFactory,
    RabbitMqOptions options,
    TimeProvider timeProvider,
    ILogger<ProjectionConsumer> logger) : BackgroundService
{
    private const string FriendsQueueName = "fookbase.posts.friend-events.v1";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.ConsumerEnabled)
        {
            logger.LogInformation("Posts integration event consumers are disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Posts RabbitMQ consumers stopped; reconnecting after backoff.");
                await Task.Delay(
                    TimeSpan.FromSeconds(options.FailureBackoffSeconds),
                    timeProvider,
                    stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            VirtualHost = options.VirtualHost,
            ClientProvidedName = "fookbase-posts-projections",
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var identityChannel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await using var friendsChannel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await identityChannel.ExchangeDeclareAsync(
            UserRegisteredIntegrationEvent.ExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await identityChannel.QueueDeclareAsync(
            UserRegisteredIntegrationEvent.PostsQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await identityChannel.QueueBindAsync(
            UserRegisteredIntegrationEvent.PostsQueueName,
            UserRegisteredIntegrationEvent.ExchangeName,
            UserRegisteredIntegrationEvent.RoutingKey,
            cancellationToken: cancellationToken);

        await friendsChannel.ExchangeDeclareAsync(
            FriendsIntegrationEventTopology.ExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await friendsChannel.QueueDeclareAsync(
            FriendsQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);
        foreach (var eventType in new[]
                 {
                     FriendRequestAcceptedIntegrationEvent.EventType,
                     FriendshipRemovedIntegrationEvent.EventType,
                     UserBlockedIntegrationEvent.EventType,
                     UserUnblockedIntegrationEvent.EventType
                 })
        {
            await friendsChannel.QueueBindAsync(
                FriendsQueueName,
                FriendsIntegrationEventTopology.ExchangeName,
                eventType,
                cancellationToken: cancellationToken);
        }

        await identityChannel.BasicQosAsync(0, 1, false, cancellationToken);
        await friendsChannel.BasicQosAsync(0, 1, false, cancellationToken);
        var identityConsumer = CreateConsumer(identityChannel, cancellationToken);
        var friendsConsumer = CreateConsumer(friendsChannel, cancellationToken);
        await identityChannel.BasicConsumeAsync(
            UserRegisteredIntegrationEvent.PostsQueueName,
            autoAck: false,
            identityConsumer,
            cancellationToken);
        await friendsChannel.BasicConsumeAsync(
            FriendsQueueName,
            autoAck: false,
            friendsConsumer,
            cancellationToken);
        await Task.Delay(Timeout.InfiniteTimeSpan, timeProvider, cancellationToken);
    }

    private AsyncEventingBasicConsumer CreateConsumer(
        IChannel channel,
        CancellationToken cancellationToken)
    {
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            var eventType = delivery.BasicProperties.Type ?? delivery.RoutingKey;
            try
            {
                var projected = await ProjectAsync(eventType, delivery.Body, cancellationToken);
                await channel.BasicAckAsync(delivery.DeliveryTag, false, cancellationToken);
                logger.LogInformation(
                    projected
                        ? "Consumed integration event {EventId} ({EventType}) into Posts projections."
                        : "Ignored duplicate integration event {EventId} ({EventType}).",
                    delivery.BasicProperties.MessageId,
                    eventType);
            }
            catch (JsonException exception)
            {
                logger.LogError(
                    exception,
                    "Rejected malformed integration event {EventId} ({EventType}).",
                    delivery.BasicProperties.MessageId,
                    eventType);
                await channel.BasicNackAsync(delivery.DeliveryTag, false, false, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Could not consume integration event {EventId} ({EventType}); it will be retried.",
                    delivery.BasicProperties.MessageId,
                    eventType);
                await Task.Delay(
                    TimeSpan.FromSeconds(options.FailureBackoffSeconds),
                    timeProvider,
                    cancellationToken);
                await channel.BasicNackAsync(delivery.DeliveryTag, false, true, cancellationToken);
            }
        };
        return consumer;
    }

    private async Task<bool> ProjectAsync(
        string eventType,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<EventProjectionStore>();
        return eventType switch
        {
            UserRegisteredIntegrationEvent.EventType => await store.ProjectAsync(
                Deserialize<UserRegisteredIntegrationEvent>(payload), cancellationToken),
            FriendRequestAcceptedIntegrationEvent.EventType => await store.ProjectAsync(
                Deserialize<FriendRequestAcceptedIntegrationEvent>(payload), cancellationToken),
            FriendshipRemovedIntegrationEvent.EventType => await store.ProjectAsync(
                Deserialize<FriendshipRemovedIntegrationEvent>(payload), cancellationToken),
            UserBlockedIntegrationEvent.EventType => await store.ProjectAsync(
                Deserialize<UserBlockedIntegrationEvent>(payload), cancellationToken),
            UserUnblockedIntegrationEvent.EventType => await store.ProjectAsync(
                Deserialize<UserUnblockedIntegrationEvent>(payload), cancellationToken),
            _ => throw new JsonException($"Unsupported integration event type '{eventType}'.")
        };
    }

    private static T Deserialize<T>(ReadOnlyMemory<byte> payload) =>
        JsonSerializer.Deserialize<T>(payload.Span)
        ?? throw new JsonException("Integration event payload was empty.");
}
