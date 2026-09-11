namespace Fookbase.Api.Modules.Stories.DTOs.Responses;

public sealed record StoryAuthorResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl);

public sealed record StoryMediaResponse(
    Guid MediaId,
    string MediaType,
    string ContentType,
    long? DurationMs,
    int? Width,
    int? Height,
    string AccessPath,
    string? PosterAccessPath);

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

public sealed record StoryTrayAuthorResponse(
    StoryAuthorResponse Author,
    bool HasUnseenStories,
    IReadOnlyList<StoryResponse> Stories);

public sealed record StoryTrayResponse(IReadOnlyList<StoryTrayAuthorResponse> Items);
