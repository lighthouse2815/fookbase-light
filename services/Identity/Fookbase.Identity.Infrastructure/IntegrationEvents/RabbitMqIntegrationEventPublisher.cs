using System.Text;
using Fookbase.Contracts.Identity;
using Fookbase.Identity.Application.Abstractions;
using RabbitMQ.Client;

namespace Fookbase.Identity.Infrastructure.IntegrationEvents;

internal sealed class RabbitMqIntegrationEventPublisher(RabbitMqOptions options)
    : IIntegrationEventPublisher
{
    public async Task PublishAsync(
        IntegrationEventMessage message,
        CancellationToken cancellationToken = default)
    {
        var factory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            VirtualHost = options.VirtualHost,
            ClientProvidedName = "fookbase-identity-outbox"
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
        await using var channel = await connection.CreateChannelAsync(
            channelOptions,
            cancellationToken);

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

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            Type = message.Type,
            MessageId = message.Id.ToString(),
            Timestamp = new AmqpTimestamp(message.OccurredAtUtc.ToUnixTimeSeconds())
        };

        await channel.BasicPublishAsync(
            UserRegisteredIntegrationEvent.ExchangeName,
            UserRegisteredIntegrationEvent.RoutingKey,
            mandatory: true,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes(message.Payload),
            cancellationToken);
    }
}
