namespace Fookbase.Api.Modules.Search.DTOs.Responses;

public sealed record SearchReelMediaResponse(
    Guid MediaId,
    long DurationMs,
    int Width,
    int Height,
    string VideoAccessPath,
    string PosterAccessPath);
