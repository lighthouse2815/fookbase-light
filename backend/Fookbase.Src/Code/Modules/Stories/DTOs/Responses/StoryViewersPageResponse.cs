namespace Fookbase.Api.Modules.Stories.DTOs.Responses;

public sealed record StoryViewersPageResponse(
    IReadOnlyList<StoryViewerResponse> Items,
    string? NextCursor);
