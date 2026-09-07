using System.Text.Json;
using Fookbase.Contracts.Identity;
using Fookbase.Contracts.Posts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Fookbase.Media.Infrastructure.IntegrationEvents;

internal sealed class ProjectionConsumer(
    IServiceScopeFactory scopeFactory, RabbitMqOptions options, TimeProvider timeProvider,
    ILogger<ProjectionConsumer> logger) : BackgroundService
{
    private const string PostsQueueName = "fookbase.media.post-attachments.v1";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.ConsumerEnabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ConsumeAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Media consumers stopped; reconnecting.");
                await Task.Delay(TimeSpan.FromSeconds(options.FailureBackoffSeconds), timeProvider, stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken token)
    {
        var factory = new ConnectionFactory
        {
            HostName = options.HostName, Port = options.Port, UserName = options.UserName,
            Password = options.Password, VirtualHost = options.VirtualHost,
            ClientProvidedName = "fookbase-media-projections", AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true
        };
        await using var connection = await factory.CreateConnectionAsync(token);
        await using var identity = await connection.CreateChannelAsync(cancellationToken: token);
        await using var posts = await connection.CreateChannelAsync(cancellationToken: token);
        await identity.ExchangeDeclareAsync(UserRegisteredIntegrationEvent.ExchangeName, ExchangeType.Topic,
            true, false, cancellationToken: token);
        await identity.QueueDeclareAsync(UserRegisteredIntegrationEvent.MediaQueueName, true, false, false,
            cancellationToken: token);
        await identity.QueueBindAsync(UserRegisteredIntegrationEvent.MediaQueueName,
            UserRegisteredIntegrationEvent.ExchangeName, UserRegisteredIntegrationEvent.RoutingKey,
            cancellationToken: token);
        await posts.ExchangeDeclareAsync("fookbase.posts.events", ExchangeType.Topic, true, false,
            cancellationToken: token);
        await posts.QueueDeclareAsync(PostsQueueName, true, false, false, cancellationToken: token);
        foreach (var type in new[] { PostMediaAttachedIntegrationEvent.EventType,
                     PostMediaDetachedIntegrationEvent.EventType })
            await posts.QueueBindAsync(PostsQueueName, "fookbase.posts.events", type, cancellationToken: token);
        await identity.BasicQosAsync(0, 1, false, token);
        await posts.BasicQosAsync(0, 1, false, token);
        await identity.BasicConsumeAsync(UserRegisteredIntegrationEvent.MediaQueueName, false,
            Consumer(identity, token), token);
        await posts.BasicConsumeAsync(PostsQueueName, false, Consumer(posts, token), token);
        await Task.Delay(Timeout.InfiniteTimeSpan, timeProvider, token);
    }

    private AsyncEventingBasicConsumer Consumer(IChannel channel, CancellationToken token)
    {
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                await ProjectAsync(delivery.BasicProperties.Type ?? delivery.RoutingKey, delivery.Body, token);
                await channel.BasicAckAsync(delivery.DeliveryTag, false, token);
            }
            catch (JsonException exception)
            {
                logger.LogError(exception, "Malformed Media projection event rejected.");
                await channel.BasicNackAsync(delivery.DeliveryTag, false, false, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Media projection failed; event will be retried.");
                await channel.BasicNackAsync(delivery.DeliveryTag, false, true, token);
            }
        };
        return consumer;
    }

    private async Task ProjectAsync(string type, ReadOnlyMemory<byte> body, CancellationToken token)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<MediaProjectionStore>();
        switch (type)
        {
            case UserRegisteredIntegrationEvent.EventType:
                await store.ProjectAsync(Deserialize<UserRegisteredIntegrationEvent>(body), token); break;
            case PostMediaAttachedIntegrationEvent.EventType:
                await store.ProjectAsync(Deserialize<PostMediaAttachedIntegrationEvent>(body), token); break;
            case PostMediaDetachedIntegrationEvent.EventType:
                await store.ProjectAsync(Deserialize<PostMediaDetachedIntegrationEvent>(body), token); break;
            default: throw new JsonException($"Unsupported event type '{type}'.");
        }
    }

    private static T Deserialize<T>(ReadOnlyMemory<byte> body) =>
        JsonSerializer.Deserialize<T>(body.Span) ?? throw new JsonException("Event body was empty.");
}
