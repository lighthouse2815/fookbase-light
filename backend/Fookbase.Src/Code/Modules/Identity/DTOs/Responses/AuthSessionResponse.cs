namespace Fookbase.Api.Modules.Identity.DTOs.Responses;

public sealed record AuthSessionResponse(Guid SessionId, string? Device, DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastSeenAtUtc, DateTimeOffset ExpiresAtUtc, bool IsCurrent);
