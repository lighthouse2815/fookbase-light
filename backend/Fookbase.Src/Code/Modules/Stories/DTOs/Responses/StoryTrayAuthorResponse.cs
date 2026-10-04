namespace Fookbase.Api.Modules.Stories.DTOs.Responses;

public sealed record StoryTrayAuthorResponse(
    StoryAuthorResponse Author,
    bool HasUnseenStories,
    IReadOnlyList<StoryResponse> Stories);
