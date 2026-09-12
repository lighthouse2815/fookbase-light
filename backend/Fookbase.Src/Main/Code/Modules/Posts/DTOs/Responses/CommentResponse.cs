namespace Fookbase.Api.Modules.Posts.DTOs.Responses;

public sealed record CommentResponse(
    Guid Id,
    Guid PostId,
    Guid AuthorUserId,
    Guid? ParentCommentId,
    string Content,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    IReadOnlyList<ContentMentionResponse>? Mentions = null);
