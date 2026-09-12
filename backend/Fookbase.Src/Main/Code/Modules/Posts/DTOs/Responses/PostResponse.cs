namespace Fookbase.Api.Modules.Posts.DTOs.Responses;

public sealed record PostResponse(
    Guid Id,
    Guid? AuthorUserId,
    string Content,
    string Privacy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    IReadOnlyList<Guid> MediaIds,
    int CommentCount,
    IReadOnlyDictionary<string, int> ReactionCounts,
    string? ViewerReaction,
    PostDisplayIdentityResponse? DisplayAuthor = null,
    string? ContainerType = null,
    IReadOnlyList<ContentMentionResponse>? Mentions = null,
    string ContentType = "standardPost");

public sealed record PostDisplayIdentityResponse(
    string Type,
    Guid Id,
    string Username,
    string Name,
    string? AvatarUrl);
