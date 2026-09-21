namespace Fookbase.Api.Modules.Posts.DTOs.Responses;

public sealed record MediaAccessResponse(
    Guid MediaId,
    string Url,
    DateTimeOffset ExpiresAtUtc,
    string MediaType,
    string ContentType);
