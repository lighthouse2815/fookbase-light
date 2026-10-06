namespace Fookbase.Api.Modules.Events.DTOs.Responses;

public sealed record EventResponse(
    Guid Id,
    string Name,
    string? Description,
    EventHostResponse DisplayHost,
    string Privacy,
    string LocationType,
    string? LocationName,
    string? Address,
    string? OnlineUrl,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    string Status,
    string? CoverUrl,
    int GoingCount,
    int InterestedCount,
    string? ViewerRsvpStatus,
    bool CanManage,
    bool CanPost,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
