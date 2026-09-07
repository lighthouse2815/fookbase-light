using System.Text;
using Fookbase.Contracts.Media;
using Fookbase.Media.Application.Abstractions;
using RabbitMQ.Client;

namespace Fookbase.Media.Infrastructure.IntegrationEvents;

internal sealed class RabbitMqIntegrationEventPublisher(RabbitMqOptions options) : IIntegrationEventPublisher
{
    public async Task PublishAsync(IntegrationEventMessage message, CancellationToken cancellationToken = default)
    {
        var factory = new ConnectionFactory
        {
            HostName = options.HostName, Port = options.Port, UserName = options.UserName,
            Password = options.Password, VirtualHost = options.VirtualHost,
            ClientProvidedName = "fookbase-media-outbox"
        };
        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(true, true), cancellationToken);
        await channel.ExchangeDeclareAsync(MediaIntegrationEventTopology.ExchangeName,
            ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        var properties = new BasicProperties
        {
            Persistent = true, ContentType = "application/json", Type = message.Type,
            MessageId = message.Id.ToString(),
            Timestamp = new AmqpTimestamp(message.OccurredAtUtc.ToUnixTimeSeconds())
        };
        await channel.BasicPublishAsync(MediaIntegrationEventTopology.ExchangeName, message.Type,
            mandatory: false, properties, Encoding.UTF8.GetBytes(message.Payload), cancellationToken);
    }
}
