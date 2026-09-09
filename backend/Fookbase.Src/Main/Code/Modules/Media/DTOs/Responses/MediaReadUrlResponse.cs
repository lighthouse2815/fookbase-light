namespace Fookbase.Api.Modules.Media.DTOs.Responses;

public sealed record MediaReadUrlResponse(
    Guid MediaId,
    string Url,
    DateTimeOffset ExpiresAtUtc);
