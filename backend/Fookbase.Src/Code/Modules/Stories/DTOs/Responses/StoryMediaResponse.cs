namespace Fookbase.Api.Modules.Stories.DTOs.Responses;

public sealed record StoryMediaResponse(
    Guid MediaId,
    string MediaType,
    string ContentType,
    long? DurationMs,
    int? Width,
    int? Height,
    string AccessPath,
    string? PosterAccessPath);
