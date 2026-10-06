namespace Fookbase.Api.Modules.Feed.DTOs.Responses;

public sealed record FeedPageResponse(
    IReadOnlyList<FeedItemResponse> Items,
    string? NextCursor,
    DateTimeOffset AsOfUtc);
