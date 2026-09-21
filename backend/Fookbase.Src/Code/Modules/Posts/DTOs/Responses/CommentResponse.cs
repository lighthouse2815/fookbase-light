namespace Fookbase.Api.Modules.Posts.DTOs.Responses;

public sealed record CommentAuthorResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl);

public sealed record CommentResponse(
    Guid Id,
    Guid PostId,
    Guid AuthorUserId,
    Guid? ParentCommentId,
    string Content,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    IReadOnlyDictionary<string, int> ReactionCounts,
    string? ViewerReaction,
    IReadOnlyList<ContentMentionResponse>? Mentions = null,
    CommentAuthorResponse? Author = null);
