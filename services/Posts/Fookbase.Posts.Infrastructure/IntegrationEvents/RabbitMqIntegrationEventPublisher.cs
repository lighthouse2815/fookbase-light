using System.Text;
using Fookbase.Contracts.Posts;
using Fookbase.Posts.Application.Abstractions;
using RabbitMQ.Client;

namespace Fookbase.Posts.Infrastructure.IntegrationEvents;

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
            ClientProvidedName = "fookbase-posts-outbox"
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
        await using var channel = await connection.CreateChannelAsync(channelOptions, cancellationToken);
        await channel.ExchangeDeclareAsync(
            PostsIntegrationEventTopology.ExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
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
            PostsIntegrationEventTopology.ExchangeName,
            message.Type,
            mandatory: false,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes(message.Payload),
            cancellationToken);
    }
}
