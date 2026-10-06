namespace Fookbase.Api.Modules.Reels.DTOs.Responses;

public sealed record ReelVideoResponse(
    Guid MediaId,
    long DurationMs,
    int Width,
    int Height,
    string ContentType,
    string VideoAccessPath,
    string PosterAccessPath);
