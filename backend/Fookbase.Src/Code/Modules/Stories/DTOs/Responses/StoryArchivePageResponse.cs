namespace Fookbase.Api.Modules.Stories.DTOs.Responses;

public sealed record StoryArchivePageResponse(
    IReadOnlyList<StoryResponse> Items,
    string? NextCursor);
