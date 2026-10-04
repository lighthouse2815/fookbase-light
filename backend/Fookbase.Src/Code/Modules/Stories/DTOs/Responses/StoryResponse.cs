namespace Fookbase.Api.Modules.Stories.DTOs.Responses;

public sealed record StoryResponse(
    Guid Id,
    StoryAuthorResponse Author,
    string? Caption,
    string Privacy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    StoryMediaResponse Media,
    bool IsViewed,
    bool CanManage,
    int? ViewerCount,
    int ReactionCount,
    string? ViewerReaction);
