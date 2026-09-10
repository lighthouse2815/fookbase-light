namespace Fookbase.Api.Modules.Groups.DTOs.Responses;

public sealed record GroupResponse(
    Guid Id,
    string Name,
    string? Description,
    string Privacy,
    Guid OwnerUserId,
    string? CoverUrl,
    int MemberCount,
    string? ViewerRole,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
