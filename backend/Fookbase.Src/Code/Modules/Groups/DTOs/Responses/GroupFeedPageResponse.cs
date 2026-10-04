namespace Fookbase.Api.Modules.Groups.DTOs.Responses;

public sealed record GroupFeedPageResponse(
    IReadOnlyList<GroupFeedItemResponse> Items,
    string? NextCursor);
