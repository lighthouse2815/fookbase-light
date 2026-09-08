using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Modules.Posts.Models;

namespace Fookbase.Api.Modules.Posts.Services;

public sealed class PostsService(
    IPostsStore store,
    IMediaReadUrlClient mediaReadUrlClient,
    PostsOptions options) : IPostsService
{
    private const int MaximumLimit = 100;

    public async Task<ApplicationResult<PostResponse>> CreatePostAsync(
        Guid actorUserId,
        string content,
        string privacy,
        IReadOnlyList<Guid> mediaIds,
        CancellationToken cancellationToken = default)
    {
        var error = ValidatePost(content, mediaIds);
        if (error is not null)
        {
            return ApplicationResult<PostResponse>.Failure(error);
        }

        if (!TryParsePrivacy(privacy, out var parsedPrivacy))
        {
            return ApplicationResult<PostResponse>.Failure(InvalidPrivacy());
        }

        return Map(await store.CreatePostAsync(
            actorUserId,
            content,
            parsedPrivacy,
            mediaIds,
            cancellationToken));
    }

    public async Task<ApplicationResult<PostResponse>> UpdatePostAsync(
        Guid actorUserId,
        Guid postId,
        string content,
        string privacy,
        IReadOnlyList<Guid> mediaIds,
        CancellationToken cancellationToken = default)
    {
        var error = ValidatePost(content, mediaIds);
        if (error is not null)
        {
            return ApplicationResult<PostResponse>.Failure(error);
        }

        if (!TryParsePrivacy(privacy, out var parsedPrivacy))
        {
            return ApplicationResult<PostResponse>.Failure(InvalidPrivacy());
        }

        return Map(await store.UpdatePostAsync(
            actorUserId,
            postId,
            content,
            parsedPrivacy,
            mediaIds,
            cancellationToken));
    }

    public async Task<ApplicationResult> DeletePostAsync(
        Guid actorUserId,
        Guid postId,
        CancellationToken cancellationToken = default) =>
        Map(await store.DeletePostAsync(actorUserId, postId, cancellationToken));

    public async Task<ApplicationResult<PostResponse>> GetPostAsync(
        Guid? viewerUserId,
        Guid postId,
        CancellationToken cancellationToken = default) =>
        Map(await store.GetPostAsync(viewerUserId, postId, cancellationToken));

    public async Task<ApplicationResult<PagedResponse<PostResponse>>> GetFeedAsync(
        Guid actorUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var error = ValidatePagination(offset, limit);
        if (error is not null)
        {
            return ApplicationResult<PagedResponse<PostResponse>>.Failure(error);
        }

        return Map(await store.GetFeedAsync(actorUserId, offset, limit, cancellationToken));
    }

    public async Task<ApplicationResult<PagedResponse<PostResponse>>> GetUserPostsAsync(
        Guid? viewerUserId,
        Guid authorUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var error = ValidatePagination(offset, limit);
        if (error is not null)
        {
            return ApplicationResult<PagedResponse<PostResponse>>.Failure(error);
        }

        return Map(await store.GetUserPostsAsync(
            viewerUserId,
            authorUserId,
            offset,
            limit,
            cancellationToken));
    }

    public async Task<ApplicationResult<CommentResponse>> CreateCommentAsync(
        Guid actorUserId,
        Guid postId,
        Guid? parentCommentId,
        string content,
        CancellationToken cancellationToken = default)
    {
        var error = ValidateContent(content, Comment.MaximumContentLength, "comment");
        if (error is not null)
        {
            return ApplicationResult<CommentResponse>.Failure(error);
        }

        return Map(await store.CreateCommentAsync(
            actorUserId,
            postId,
            parentCommentId,
            content,
            cancellationToken));
    }

    public async Task<ApplicationResult<CommentResponse>> UpdateCommentAsync(
        Guid actorUserId,
        Guid commentId,
        string content,
        CancellationToken cancellationToken = default)
    {
        var error = ValidateContent(content, Comment.MaximumContentLength, "comment");
        if (error is not null)
        {
            return ApplicationResult<CommentResponse>.Failure(error);
        }

        return Map(await store.UpdateCommentAsync(actorUserId, commentId, content, cancellationToken));
    }

    public async Task<ApplicationResult> DeleteCommentAsync(
        Guid actorUserId,
        Guid commentId,
        CancellationToken cancellationToken = default) =>
        Map(await store.DeleteCommentAsync(actorUserId, commentId, cancellationToken));

    public async Task<ApplicationResult<PagedResponse<CommentResponse>>> GetCommentsAsync(
        Guid? viewerUserId,
        Guid postId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var error = ValidatePagination(offset, limit);
        if (error is not null)
        {
            return ApplicationResult<PagedResponse<CommentResponse>>.Failure(error);
        }

        return Map(await store.GetCommentsAsync(
            viewerUserId,
            postId,
            offset,
            limit,
            cancellationToken));
    }

    public async Task<ApplicationResult<PostResponse>> SetReactionAsync(
        Guid actorUserId,
        Guid postId,
        string reactionType,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<ReactionType>(reactionType, true, out var parsedReaction) ||
            !Enum.IsDefined(parsedReaction))
        {
            return ApplicationResult<PostResponse>.Failure(new ApplicationError(
                "invalid_reaction_type",
                "Reaction type must be one of: like, love, haha, wow, sad, angry.",
                ApplicationErrorType.Validation));
        }

        return Map(await store.SetReactionAsync(
            actorUserId,
            postId,
            parsedReaction,
            cancellationToken));
    }

    public async Task<ApplicationResult<PostResponse>> RemoveReactionAsync(
        Guid actorUserId,
        Guid postId,
        CancellationToken cancellationToken = default) =>
        Map(await store.RemoveReactionAsync(actorUserId, postId, cancellationToken));

    public async Task<ApplicationResult<MediaAccessResponse>> GetMediaAccessAsync(
        Guid actorUserId, Guid postId, Guid mediaId, CancellationToken cancellationToken = default)
    {
        var access = await store.AuthorizeMediaAccessAsync(actorUserId, postId, mediaId, cancellationToken);
        if (access != PostsStoreError.None)
            return ApplicationResult<MediaAccessResponse>.Failure(ToApplicationError(access));
        var url = await mediaReadUrlClient.CreateReadUrlAsync(mediaId, cancellationToken);
        return url is null
            ? ApplicationResult<MediaAccessResponse>.Failure(new ApplicationError(
                "media_unavailable", "The attached media is unavailable.", ApplicationErrorType.NotFound))
            : ApplicationResult<MediaAccessResponse>.Success(
                new MediaAccessResponse(url.MediaId, url.Url, url.ExpiresAtUtc));
    }

    private static bool TryParsePrivacy(string privacy, out PostPrivacy parsedPrivacy) =>
        Enum.TryParse(privacy, true, out parsedPrivacy) && Enum.IsDefined(parsedPrivacy);

    private static ApplicationError InvalidPrivacy() =>
        new(
            "invalid_post_privacy",
            "Post privacy must be one of: public, friends, onlyMe.",
            ApplicationErrorType.Validation);

    private static ApplicationError? ValidateContent(string? content, int maximumLength, string resource)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Trim().Length > maximumLength)
        {
            return new ApplicationError(
                $"invalid_{resource}_content",
                $"The {resource} content must contain between 1 and {maximumLength} characters.",
                ApplicationErrorType.Validation);
        }

        return null;
    }

    private ApplicationError? ValidatePost(string? content, IReadOnlyList<Guid> mediaIds)
    {
        if (string.IsNullOrWhiteSpace(content) && mediaIds.Count == 0)
            return new ApplicationError("empty_post", "A post requires content or media.", ApplicationErrorType.Validation);
        if ((content?.Trim().Length ?? 0) > Post.MaximumContentLength)
            return new ApplicationError("invalid_post_content",
                $"Post content cannot exceed {Post.MaximumContentLength} characters.", ApplicationErrorType.Validation);
        if (mediaIds.Count > options.MaximumAttachments)
            return new ApplicationError("too_many_attachments",
                $"A post can contain at most {options.MaximumAttachments} media attachments.", ApplicationErrorType.Validation);
        if (mediaIds.Count != mediaIds.Distinct().Count())
            return new ApplicationError("duplicate_attachment", "Media attachments cannot be duplicated.", ApplicationErrorType.Validation);
        return null;
    }

    private static ApplicationError? ValidatePagination(int offset, int limit)
    {
        if (offset < 0 || limit < 1 || limit > MaximumLimit)
        {
            return new ApplicationError(
                "invalid_pagination",
                $"Offset must be non-negative and limit must be between 1 and {MaximumLimit}.",
                ApplicationErrorType.Validation);
        }

        return null;
    }

    private static ApplicationResult<T> Map<T>(PostsStoreResult<T> result) =>
        result.Succeeded
            ? ApplicationResult<T>.Success(result.Value!)
            : ApplicationResult<T>.Failure(ToApplicationError(result.Error));

    private static ApplicationResult Map(PostsStoreError error) =>
        error == PostsStoreError.None
            ? ApplicationResult.Success()
            : ApplicationResult.Failure(ToApplicationError(error));

    private static ApplicationError ToApplicationError(PostsStoreError error) => error switch
    {
        PostsStoreError.UserNotFound => new(
            "user_not_found", "The user was not found.", ApplicationErrorType.NotFound),
        PostsStoreError.PostNotFound => new(
            "post_not_found", "The post was not found.", ApplicationErrorType.NotFound),
        PostsStoreError.CommentNotFound => new(
            "comment_not_found", "The comment was not found.", ApplicationErrorType.NotFound),
        PostsStoreError.ParentCommentNotFound => new(
            "parent_comment_not_found", "The parent comment was not found.", ApplicationErrorType.NotFound),
        PostsStoreError.Forbidden => new(
            "forbidden", "You are not allowed to perform this operation.", ApplicationErrorType.Forbidden),
        PostsStoreError.RelationshipBlocked => new(
            "relationship_unavailable", "This interaction is unavailable.", ApplicationErrorType.Conflict),
        PostsStoreError.InvalidParentComment => new(
            "invalid_parent_comment", "A reply can only target a top-level comment on the same post.", ApplicationErrorType.Validation),
        PostsStoreError.InvalidMedia => new(
            "invalid_media", "Every attachment must be ready.", ApplicationErrorType.Conflict),
        PostsStoreError.MediaNotOwned => new(
            "media_not_owned", "Only the media owner can attach it.", ApplicationErrorType.Forbidden),
        PostsStoreError.MediaNotAttached => new(
            "media_not_attached", "The media is not attached to this post.", ApplicationErrorType.NotFound),
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, null)
    };
}
