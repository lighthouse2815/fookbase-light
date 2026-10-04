namespace Fookbase.Api.Modules.Search.DTOs.Responses;

public sealed record SearchEventResponse(
    Guid EventId,
    string Name,
    string HostType,
    Guid HostId,
    string HostName,
    DateTimeOffset StartsAtUtc,
    string LocationType,
    string? LocationName,
    string? CoverUrl,
    int GoingCount,
    int InterestedCount);
