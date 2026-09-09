using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Posts.DTOs;
using Fookbase.Api.Modules.Posts.Common;
using Fookbase.Api.Modules.Posts.Services;
using MediaApplicationError = Fookbase.Api.Modules.Media.Services.ApplicationError;

namespace Fookbase.Api.Application;

public sealed class PostsUseCase(
    PostsService postsService,
    FriendsService friendsService,
    Fookbase.Api.Modules.Media.Services.IMediaService mediaService)
{
    public async Task<ApplicationResult<PostResponse>> CreatePostAsync(
        Guid actorUserId,
        string content,
        string privacy,
        IReadOnlyList<Guid> mediaIds,
        CancellationToken cancellationToken = default)
    {
        var input = postsService.ValidatePostRequest(content, privacy, mediaIds);
        if (!input.Succeeded)
        {
            return ApplicationResult<PostResponse>.Failure(input.Error!);
        }

        var mediaError = await ValidatePostMediaAsync(actorUserId, mediaIds, cancellationToken);
        if (mediaError is not null)
        {
            return ApplicationResult<PostResponse>.Failure(mediaError);
        }

        var result = await postsService.CreatePostAsync(
            actorUserId, content, privacy, mediaIds, cancellationToken);
        if (!result.Succeeded)
        {
            return result;
        }

        var references = await mediaService.SynchronizePostReferencesAsync(
            actorUserId, result.Value!.Id, mediaIds, cancellationToken);
        return references.Succeeded
            ? result
            : ApplicationResult<PostResponse>.Failure(ToPostError(references.Error!));
    }

    public async Task<ApplicationResult<PostResponse>> UpdatePostAsync(
        Guid actorUserId,
        Guid postId,
        string content,
        string privacy,
        IReadOnlyList<Guid> mediaIds,
        CancellationToken cancellationToken = default)
    {
        var input = postsService.ValidatePostRequest(content, privacy, mediaIds);
        if (!input.Succeeded)
        {
            return ApplicationResult<PostResponse>.Failure(input.Error!);
        }

        var ownership = await postsService.EnsurePostOwnerAsync(actorUserId, postId, cancellationToken);
        if (!ownership.Succeeded)
        {
            return ApplicationResult<PostResponse>.Failure(ownership.Error!);
        }

        var mediaError = await ValidatePostMediaAsync(actorUserId, mediaIds, cancellationToken);
        if (mediaError is not null)
        {
            return ApplicationResult<PostResponse>.Failure(mediaError);
        }

        var result = await postsService.UpdatePostAsync(
            actorUserId, postId, content, privacy, mediaIds, cancellationToken);
        if (!result.Succeeded)
        {
            return result;
        }

        var references = await mediaService.SynchronizePostReferencesAsync(
            actorUserId, postId, mediaIds, cancellationToken);
        return references.Succeeded
            ? result
            : ApplicationResult<PostResponse>.Failure(ToPostError(references.Error!));
    }

    public async Task<ApplicationResult> DeletePostAsync(
        Guid actorUserId,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var result = await postsService.DeletePostAsync(actorUserId, postId, cancellationToken);
        if (!result.Succeeded)
        {
            return result;
        }

        await mediaService.RemovePostReferencesAsync(postId, cancellationToken);
        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult<PostResponse>> GetPostAsync(
        Guid? viewerUserId,
        Guid postId,
        CancellationToken cancellationToken = default) =>
        await postsService.GetPostAsync(
            await CreateViewerContextAsync(viewerUserId, cancellationToken), postId, cancellationToken);

    public async Task<ApplicationResult<PagedResponse<PostResponse>>> GetFeedAsync(
        Guid viewerUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default) =>
        await postsService.GetFeedAsync(
            await CreateRequiredViewerContextAsync(viewerUserId, cancellationToken), offset, limit, cancellationToken);

    public async Task<ApplicationResult<PagedResponse<PostResponse>>> GetUserPostsAsync(
        Guid? viewerUserId,
        Guid authorUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default) =>
        await postsService.GetUserPostsAsync(
            await CreateViewerContextAsync(viewerUserId, cancellationToken),
            authorUserId,
            offset,
            limit,
            cancellationToken);

    public async Task<ApplicationResult<CommentResponse>> CreateCommentAsync(
        Guid actorUserId,
        Guid postId,
        Guid? parentCommentId,
        string content,
        CancellationToken cancellationToken = default) =>
        await postsService.CreateCommentAsync(
            await CreateRequiredViewerContextAsync(actorUserId, cancellationToken),
            postId,
            parentCommentId,
            content,
            cancellationToken);

    public Task<ApplicationResult<CommentResponse>> UpdateCommentAsync(
        Guid actorUserId,
        Guid commentId,
        string content,
        CancellationToken cancellationToken = default) =>
        postsService.UpdateCommentAsync(actorUserId, commentId, content, cancellationToken);

    public Task<ApplicationResult> DeleteCommentAsync(
        Guid actorUserId,
        Guid commentId,
        CancellationToken cancellationToken = default) =>
        postsService.DeleteCommentAsync(actorUserId, commentId, cancellationToken);

    public async Task<ApplicationResult<PagedResponse<CommentResponse>>> GetCommentsAsync(
        Guid? viewerUserId,
        Guid postId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default) =>
        await postsService.GetCommentsAsync(
            await CreateViewerContextAsync(viewerUserId, cancellationToken),
            postId,
            offset,
            limit,
            cancellationToken);

    public async Task<ApplicationResult<PostResponse>> SetReactionAsync(
        Guid actorUserId,
        Guid postId,
        string reactionType,
        CancellationToken cancellationToken = default) =>
        await postsService.SetReactionAsync(
            await CreateRequiredViewerContextAsync(actorUserId, cancellationToken),
            postId,
            reactionType,
            cancellationToken);

    public async Task<ApplicationResult<PostResponse>> RemoveReactionAsync(
        Guid actorUserId,
        Guid postId,
        CancellationToken cancellationToken = default) =>
        await postsService.RemoveReactionAsync(
            await CreateRequiredViewerContextAsync(actorUserId, cancellationToken), postId, cancellationToken);

    public async Task<ApplicationResult<MediaAccessResponse>> GetMediaAccessAsync(
        Guid actorUserId,
        Guid postId,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        var access = await postsService.AuthorizeMediaAccessAsync(
            await CreateRequiredViewerContextAsync(actorUserId, cancellationToken), postId, mediaId, cancellationToken);
        if (!access.Succeeded)
        {
            return ApplicationResult<MediaAccessResponse>.Failure(access.Error!);
        }

        var readUrl = await mediaService.CreateReadUrlAsync(mediaId, cancellationToken);
        return readUrl.Succeeded
            ? ApplicationResult<MediaAccessResponse>.Success(
                new MediaAccessResponse(
                    readUrl.Value!.MediaId,
                    readUrl.Value.Url,
                    readUrl.Value.ExpiresAtUtc))
            : ApplicationResult<MediaAccessResponse>.Failure(new ApplicationError(
                "media_unavailable", "The attached media is unavailable.", ApplicationErrorType.NotFound));
    }

    private async Task<PostViewerContext?> CreateViewerContextAsync(
        Guid? viewerUserId,
        CancellationToken cancellationToken)
    {
        if (viewerUserId is null)
        {
            return null;
        }

        return await CreateRequiredViewerContextAsync(viewerUserId.Value, cancellationToken);
    }

    private async Task<PostViewerContext> CreateRequiredViewerContextAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken)
    {
        var relationships = await friendsService.GetAccessSnapshotAsync(viewerUserId, cancellationToken);
        return new PostViewerContext(
            viewerUserId,
            relationships.FriendUserIds,
            relationships.BlockedUserIds);
    }

    private async Task<ApplicationError?> ValidatePostMediaAsync(
        Guid ownerUserId,
        IReadOnlyCollection<Guid> mediaIds,
        CancellationToken cancellationToken)
    {
        var result = await mediaService.ValidatePostMediaAsync(ownerUserId, mediaIds, cancellationToken);
        return result.Succeeded ? null : ToPostError(result.Error!);
    }

    private static ApplicationError ToPostError(MediaApplicationError error) =>
        new(error.Code, error.Message, (ApplicationErrorType)(int)error.Type);
}
