using Fookbase.Posts.Application.Posts;
using Fookbase.Posts.Domain.Entities;

namespace Fookbase.Posts.Application.Abstractions;

public enum PostsStoreError
{
    None,
    UserNotFound,
    PostNotFound,
    CommentNotFound,
    ParentCommentNotFound,
    Forbidden,
    RelationshipBlocked,
    InvalidParentComment
}

public sealed record PostsStoreResult<T>(T? Value, PostsStoreError Error)
{
    public bool Succeeded => Error == PostsStoreError.None;

    public static PostsStoreResult<T> Success(T value) =>
        new(value, PostsStoreError.None);

    public static PostsStoreResult<T> Failure(PostsStoreError error) =>
        new(default, error);
}

public interface IPostsStore
{
    Task<PostsStoreResult<PostResponse>> CreatePostAsync(
        Guid authorUserId,
        string content,
        PostPrivacy privacy,
        CancellationToken cancellationToken = default);

    Task<PostsStoreResult<PostResponse>> UpdatePostAsync(
        Guid actorUserId,
        Guid postId,
        string content,
        PostPrivacy privacy,
        CancellationToken cancellationToken = default);

    Task<PostsStoreError> DeletePostAsync(
        Guid actorUserId,
        Guid postId,
        CancellationToken cancellationToken = default);

    Task<PostsStoreResult<PostResponse>> GetPostAsync(
        Guid? viewerUserId,
        Guid postId,
        CancellationToken cancellationToken = default);

    Task<PostsStoreResult<PagedResponse<PostResponse>>> GetFeedAsync(
        Guid viewerUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    Task<PostsStoreResult<PagedResponse<PostResponse>>> GetUserPostsAsync(
        Guid? viewerUserId,
        Guid authorUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    Task<PostsStoreResult<CommentResponse>> CreateCommentAsync(
        Guid authorUserId,
        Guid postId,
        Guid? parentCommentId,
        string content,
        CancellationToken cancellationToken = default);

    Task<PostsStoreResult<CommentResponse>> UpdateCommentAsync(
        Guid actorUserId,
        Guid commentId,
        string content,
        CancellationToken cancellationToken = default);

    Task<PostsStoreError> DeleteCommentAsync(
        Guid actorUserId,
        Guid commentId,
        CancellationToken cancellationToken = default);

    Task<PostsStoreResult<PagedResponse<CommentResponse>>> GetCommentsAsync(
        Guid? viewerUserId,
        Guid postId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    Task<PostsStoreResult<PostResponse>> SetReactionAsync(
        Guid actorUserId,
        Guid postId,
        ReactionType reactionType,
        CancellationToken cancellationToken = default);

    Task<PostsStoreResult<PostResponse>> RemoveReactionAsync(
        Guid actorUserId,
        Guid postId,
        CancellationToken cancellationToken = default);
}
