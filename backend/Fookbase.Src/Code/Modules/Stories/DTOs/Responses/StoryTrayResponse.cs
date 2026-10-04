namespace Fookbase.Api.Modules.Stories.DTOs.Responses;

public sealed record StoryTrayResponse(IReadOnlyList<StoryTrayAuthorResponse> Items);
