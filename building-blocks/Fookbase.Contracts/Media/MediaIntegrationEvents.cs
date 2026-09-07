namespace Fookbase.Contracts.Media;

public static class MediaIntegrationEventTopology
{
    public const string ExchangeName = "fookbase.media.events";
    public const string PostsQueueName = "fookbase.posts.media-events.v1";
}

public sealed record MediaReadyIntegrationEvent(
    Guid EventId,
    Guid MediaId,
    Guid OwnerUserId,
    string MediaType,
    string ContentType,
    long SizeBytes,
    DateTimeOffset OccurredAtUtc)
{
    public const string EventType = "media.asset.ready.v1";
}

public sealed record MediaDeletedIntegrationEvent(
    Guid EventId,
    Guid MediaId,
    Guid OwnerUserId,
    DateTimeOffset OccurredAtUtc)
{
    public const string EventType = "media.asset.deleted.v1";
}
