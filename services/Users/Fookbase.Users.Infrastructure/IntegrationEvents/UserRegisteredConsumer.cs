using System.Text.Json;
using Fookbase.Contracts.Identity;
using Fookbase.Users.Application.Registrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Fookbase.Users.Infrastructure.IntegrationEvents;

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
            logger.LogInformation("Users RabbitMQ consumer is disabled.");
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
                logger.LogError(exception, "Users RabbitMQ consumer stopped; reconnecting after backoff.");
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
            ClientProvidedName = "fookbase-users-user-registered",
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
            UserRegisteredIntegrationEvent.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            UserRegisteredIntegrationEvent.QueueName,
            UserRegisteredIntegrationEvent.ExchangeName,
            UserRegisteredIntegrationEvent.RoutingKey,
            cancellationToken: cancellationToken);
        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken);

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
                var created = await handler.HandleAsync(integrationEvent, cancellationToken);

                await channel.BasicAckAsync(
                    delivery.DeliveryTag,
                    multiple: false,
                    cancellationToken);
                logger.LogInformation(
                    created
                        ? "Consumed integration event {EventId} ({EventType}) and created a user profile."
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
                await channel.BasicNackAsync(
                    delivery.DeliveryTag,
                    multiple: false,
                    requeue: false,
                    cancellationToken);
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
                await channel.BasicNackAsync(
                    delivery.DeliveryTag,
                    multiple: false,
                    requeue: true,
                    cancellationToken);
            }
        };

        await channel.BasicConsumeAsync(
            UserRegisteredIntegrationEvent.QueueName,
            autoAck: false,
            consumer,
            cancellationToken);
        await Task.Delay(Timeout.InfiniteTimeSpan, timeProvider, cancellationToken);
    }
}
