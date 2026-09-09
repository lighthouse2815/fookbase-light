namespace Fookbase.Api.Shared.Contracts.Media;

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
