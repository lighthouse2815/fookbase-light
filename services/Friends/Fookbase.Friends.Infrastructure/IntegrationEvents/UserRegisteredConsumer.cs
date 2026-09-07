using System.Text.Json;
using Fookbase.Contracts.Identity;
using Fookbase.Friends.Application.Registrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Fookbase.Friends.Infrastructure.IntegrationEvents;

internal sealed class UserRegisteredConsumer(
    IServiceScopeFactory scopeFactory,
    RabbitMqOptions options,
    TimeProvider timeProvider,
    ILogger<UserRegisteredConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.ConsumerEnabled)
        {
            logger.LogInformation("Friends user registration consumer is disabled.");
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
                logger.LogError(exception, "Friends RabbitMQ consumer stopped; reconnecting after backoff.");
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
            ClientProvidedName = "fookbase-friends-user-registered",
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            UserRegisteredIntegrationEvent.ExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            UserRegisteredIntegrationEvent.FriendsQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            UserRegisteredIntegrationEvent.FriendsQueueName,
            UserRegisteredIntegrationEvent.ExchangeName,
            UserRegisteredIntegrationEvent.RoutingKey,
            cancellationToken: cancellationToken);
        await channel.BasicQosAsync(0, 1, false, cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                var integrationEvent = JsonSerializer.Deserialize<UserRegisteredIntegrationEvent>(
                    delivery.Body.Span)
                    ?? throw new JsonException("User registered event payload was empty.");
                using var scope = scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<IUserRegisteredEventHandler>();
                var projected = await handler.HandleAsync(integrationEvent, cancellationToken);
                await channel.BasicAckAsync(delivery.DeliveryTag, false, cancellationToken);
                logger.LogInformation(
                    projected
                        ? "Consumed integration event {EventId} ({EventType}) and projected a known user."
                        : "Ignored duplicate integration event {EventId} ({EventType}).",
                    integrationEvent.EventId,
                    UserRegisteredIntegrationEvent.EventType);
            }
            catch (JsonException exception)
            {
                logger.LogError(
                    exception,
                    "Rejected malformed integration event {EventId} ({EventType}).",
                    delivery.BasicProperties.MessageId,
                    delivery.BasicProperties.Type);
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
                    delivery.BasicProperties.Type);
                await Task.Delay(
                    TimeSpan.FromSeconds(options.FailureBackoffSeconds),
                    timeProvider,
                    cancellationToken);
                await channel.BasicNackAsync(delivery.DeliveryTag, false, true, cancellationToken);
            }
        };

        await channel.BasicConsumeAsync(
            UserRegisteredIntegrationEvent.FriendsQueueName,
            autoAck: false,
            consumer,
            cancellationToken);
        await Task.Delay(Timeout.InfiniteTimeSpan, timeProvider, cancellationToken);
    }
}
