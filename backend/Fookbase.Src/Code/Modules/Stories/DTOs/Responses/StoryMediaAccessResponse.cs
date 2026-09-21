namespace Fookbase.Api.Modules.Stories.DTOs.Responses;

public sealed record StoryMediaAccessResponse(
    Guid MediaId,
    string Url,
    DateTimeOffset ExpiresAtUtc,
    string MediaType,
    string ContentType);
