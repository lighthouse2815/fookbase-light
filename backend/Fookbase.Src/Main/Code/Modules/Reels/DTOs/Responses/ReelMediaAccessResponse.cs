namespace Fookbase.Api.Modules.Reels.DTOs.Responses;

public sealed record ReelMediaAccessResponse(
    Guid MediaId,
    string Url,
    DateTimeOffset ExpiresAtUtc,
    string MediaType,
    string ContentType);
