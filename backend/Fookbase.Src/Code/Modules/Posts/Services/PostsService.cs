using Fookbase.Api.Modules.Friends.Domain.Enums;
using Fookbase.Api.Modules.Notifications.Domain.Enums;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Posts.Config;
using Fookbase.Api.Modules.Groups.Services;
using Fookbase.Api.Modules.Pages.Services;
using Fookbase.Api.Modules.Events.Services;
using Fookbase.Api.Modules.Photos.Entities;
using Fookbase.Api.Modules.Photos.Services;
using Fookbase.Api.Modules.Posts.DTOs.Responses;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Notifications.Entities;
using Fookbase.Api.Modules.Notifications.Services;
using Fookbase.Api.Modules.Friends.Entities;
using Fookbase.Api.Modules.Users.Common;
using Fookbase.Api.Shared.ErrorHandling;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Services;

public sealed class PostsService(
    FookbaseDbContext dbContext,
    NotificationService notificationService,
    GroupPostAccessService groupPostAccessService,
    PagePostAccessService pagePostAccessService,
    EventAccessService eventPostAccessService,
    PhotosService photosService,
    TimeProvider timeProvider,
    PostsOptions options)
{
    private const int MaximumLimit = 100;
    private static readonly HashSet<string> TextBackgrounds =
        ["purple", "pink", "midnight", "sunset", "ocean", "neon"];

    public async Task<ApplicationResult<PostResponse>> CreatePostAsync(
        Guid actorUserId,
        string content,
        string privacy,
        IReadOnlyList<Guid> mediaIds,
        string? textBackground = null,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidatePostRequest(content, privacy, mediaIds);
        if (!validation.Succeeded)
        {
            return ApplicationResult<PostResponse>.Failure(validation.Error!);
        }

        TryParsePrivacy(privacy, out var parsedPrivacy);

        var textBackgroundError = ValidateTextBackground(textBackground, content, mediaIds);
        if (textBackgroundError is not null)
        {
            return ApplicationResult<PostResponse>.Failure(textBackgroundError);
        }

        return Map(await CreatePostCoreAsync(
            actorUserId,
            content,
            parsedPrivacy,
            mediaIds,
            cancellationToken,
            textBackground: textBackground));
    }

    public async Task<ApplicationResult<PostResponse>> CreatePostInGroupAsync(
        Guid actorUserId,
        Guid groupId,
        string content,
        IReadOnlyList<Guid> mediaIds,
        CancellationToken cancellationToken = default,
        string? textBackground = null)
    {
        var validation = ValidatePostRequest(content, "public", mediaIds);
        if (!validation.Succeeded)
        {
            return ApplicationResult<PostResponse>.Failure(validation.Error!);
        }

        var textBackgroundError = ValidateTextBackground(textBackground, content, mediaIds);
        if (textBackgroundError is not null)
        {
            return ApplicationResult<PostResponse>.Failure(textBackgroundError);
        }

        return Map(await CreatePostInContainerCoreAsync(
            actorUserId,
            content,
            PostPrivacy.PUBLIC,
            PostContainerType.GROUP,
            groupId,
            mediaIds,
            cancellationToken,
            textBackground: textBackground));
    }

    public async Task<ApplicationResult<PostResponse>> UpdatePostAsync(
        Guid actorUserId,
        Guid postId,
        string content,
        string privacy,
        IReadOnlyList<Guid> mediaIds,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidatePostRequest(content, privacy, mediaIds);
        if (!validation.Succeeded)
        {
            return ApplicationResult<PostResponse>.Failure(validation.Error!);
        }

        TryParsePrivacy(privacy, out var parsedPrivacy);

        return Map(await UpdatePostCoreAsync(
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
        Map(await DeletePostCoreAsync(actorUserId, postId, cancellationToken));

    public async Task<ApplicationResult> DeletePostForModerationAsync(
        Guid postId,
        CancellationToken cancellationToken = default) =>
        Map(await DeletePostCoreAsync(null, postId, cancellationToken));

    public async Task<ApplicationResult<PostResponse>> SetPostPinnedAsync(
        Guid actorUserId,
        Guid postId,
        bool isPinned,
        CancellationToken cancellationToken = default)
    {
        var post = await dbContext.Posts.SingleOrDefaultAsync(
            item => item.Id == postId && item.DeletedAtUtc == null,
            cancellationToken);
        if (post is null)
        {
            return ApplicationResult<PostResponse>.Failure(ToApplicationError(PostsServiceError.POST_NOT_FOUND));
        }

        if (post.AuthorUserId != actorUserId || post.ContainerType != PostContainerType.PROFILE)
        {
            return ApplicationResult<PostResponse>.Failure(ToApplicationError(PostsServiceError.FORBIDDEN));
        }

        if (isPinned)
        {
            await dbContext.Posts
                .Where(item => item.AuthorUserId == actorUserId &&
                    item.ContainerType == PostContainerType.PROFILE &&
                    item.DeletedAtUtc == null && item.Id != postId && item.IsPinned)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(item => item.IsPinned, false),
                    cancellationToken);
        }

        post.SetPinned(isPinned);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult<PostResponse>.Success(
            (await LoadResponsesAsync([post], actorUserId, cancellationToken))[0]);
    }

    public ApplicationResult ValidatePostRequest(
        string content,
        string privacy,
        IReadOnlyList<Guid> mediaIds)
    {
        var error = ValidatePost(content, mediaIds);
        if (error is not null)
        {
            return ApplicationResult.Failure(error);
        }

        return TryParsePrivacy(privacy, out _)
            ? ApplicationResult.Success()
            : ApplicationResult.Failure(InvalidPrivacy());
    }

    private static ApplicationError? ValidateTextBackground(
        string? textBackground,
        string content,
        IReadOnlyList<Guid> mediaIds) =>
        !string.IsNullOrWhiteSpace(textBackground) &&
        (!TextBackgrounds.Contains(textBackground) || mediaIds.Count > 0 || string.IsNullOrWhiteSpace(content))
            ? new ApplicationError(
                "invalid_text_background",
                "Text backgrounds require a text-only post and a supported background.",
                ApplicationErrorType.VALIDATION)
            : null;

    public async Task<ApplicationResult> EnsurePostOwnerAsync(
        Guid actorUserId,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var post = await dbContext.Posts.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == postId && item.DeletedAtUtc == null,
            cancellationToken);
        if (post is null)
        {
            return ApplicationResult.Failure(ToApplicationError(PostsServiceError.POST_NOT_FOUND));
        }

        if (post.ContainerType == PostContainerType.PAGE)
        {
            return await pagePostAccessService.CanCreatePostAsync(post.ContainerId, actorUserId, cancellationToken)
                ? ApplicationResult.Success()
                : ApplicationResult.Failure(ToApplicationError(PostsServiceError.FORBIDDEN));
        }

        return post.AuthorUserId == actorUserId
            ? ApplicationResult.Success()
            : ApplicationResult.Failure(ToApplicationError(PostsServiceError.FORBIDDEN));
    }

    public async Task<ApplicationResult<PostResponse>> GetPostAsync(
        PostViewerContext? viewer,
        Guid postId,
        CancellationToken cancellationToken = default) =>
        Map(await GetPostCoreAsync(viewer, postId, cancellationToken));

    public async Task<ApplicationResult<PagedResponse<PostResponse>>> GetFeedAsync(
        PostViewerContext viewer,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var error = ValidatePagination(offset, limit);
        if (error is not null)
        {
            return ApplicationResult<PagedResponse<PostResponse>>.Failure(error);
        }

        return Map(await GetFeedCoreAsync(viewer, offset, limit, cancellationToken));
    }

    public async Task<ApplicationResult<PagedResponse<PostResponse>>> GetUserPostsAsync(
        PostViewerContext? viewer,
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

        return Map(await GetUserPostsCoreAsync(
            viewer,
            authorUserId,
            offset,
            limit,
            cancellationToken));
    }

    public async Task<ApplicationResult<PagedResponse<PostResponse>>> SearchPostsAsync(
        PostViewerContext viewer,
        string? query,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var error = ValidatePagination(offset, limit);
        if (error is not null)
        {
            return ApplicationResult<PagedResponse<PostResponse>>.Failure(error);
        }

        var normalizedQuery = query?.Trim().ToLowerInvariant();
        var posts = VisiblePosts(viewer);
        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            posts = posts.Where(post => post.Content.ToLower().Contains(normalizedQuery));
        }

        var total = await posts.CountAsync(cancellationToken);
        var items = await posts
            .OrderByDescending(post => post.CreatedAtUtc)
            .ThenByDescending(post => post.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
        var responses = await LoadResponsesAsync(items, viewer.UserId, cancellationToken);
        return ApplicationResult<PagedResponse<PostResponse>>.Success(
            new PagedResponse<PostResponse>(responses, offset, limit, total));
    }

    public async Task<ApplicationResult<CommentResponse>> CreateCommentAsync(
        PostViewerContext actor,
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

        return Map(await CreateCommentCoreAsync(
            actor.UserId,
            postId,
            parentCommentId,
            content,
            actor,
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

        return Map(await UpdateCommentCoreAsync(actorUserId, commentId, content, cancellationToken));
    }

    public async Task<ApplicationResult> DeleteCommentAsync(
        Guid actorUserId,
        Guid commentId,
        CancellationToken cancellationToken = default) =>
        Map(await DeleteCommentCoreAsync(actorUserId, commentId, cancellationToken));

    public async Task<ApplicationResult<PagedResponse<CommentResponse>>> GetCommentsAsync(
        PostViewerContext? viewer,
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

        return Map(await GetCommentsCoreAsync(
            viewer,
            postId,
            offset,
            limit,
            cancellationToken));
    }

    public async Task<CommentResponse?> GetCommentResponseAsync(
        Guid commentId,
        Guid? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var comment = await dbContext.Comments.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == commentId && item.DeletedAtUtc == null,
            cancellationToken);
        return comment is null
            ? null
            : (await LoadCommentResponsesAsync([comment], viewerUserId, cancellationToken))[0];
    }

    public async Task<ApplicationResult<PostResponse>> SetReactionAsync(
        PostViewerContext actor,
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
                ApplicationErrorType.VALIDATION));
        }

        return Map(await SetReactionCoreAsync(
            actor.UserId,
            postId,
            parsedReaction,
            actor,
            cancellationToken));
    }

    public async Task<ApplicationResult<PostResponse>> RemoveReactionAsync(
        PostViewerContext actor,
        Guid postId,
        CancellationToken cancellationToken = default) =>
        Map(await RemoveReactionCoreAsync(actor.UserId, postId, actor, cancellationToken));

    public async Task<ApplicationResult<PagedResponse<PostReactionResponse>>> GetReactionsAsync(
        PostViewerContext viewer,
        Guid postId,
        string? reactionType,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var paginationError = ValidatePagination(offset, limit);
        if (paginationError is not null)
        {
            return ApplicationResult<PagedResponse<PostReactionResponse>>.Failure(paginationError);
        }

        ReactionType? parsedReaction = null;
        if (!string.IsNullOrWhiteSpace(reactionType) &&
            (!Enum.TryParse<ReactionType>(reactionType, true, out var parsed) || !Enum.IsDefined(parsed)))
        {
            return ApplicationResult<PagedResponse<PostReactionResponse>>.Failure(new ApplicationError(
                "invalid_reaction_type",
                "Reaction type must be one of: like, love, haha, wow, sad, angry.",
                ApplicationErrorType.VALIDATION));
        }
        else if (!string.IsNullOrWhiteSpace(reactionType))
        {
            parsedReaction = Enum.Parse<ReactionType>(reactionType, true);
        }

        return Map(await GetReactionsCoreAsync(
            viewer, postId, parsedReaction, offset, limit, cancellationToken));
    }

    public async Task<ApplicationResult<CommentResponse>> SetCommentReactionAsync(
        PostViewerContext actor,
        Guid commentId,
        string reactionType,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<ReactionType>(reactionType, true, out var parsedReaction) ||
            !Enum.IsDefined(parsedReaction))
        {
            return ApplicationResult<CommentResponse>.Failure(new ApplicationError(
                "invalid_reaction_type",
                "Reaction type must be one of: like, love, haha, wow, sad, angry.",
                ApplicationErrorType.VALIDATION));
        }

        var error = await ChangeCommentReactionAsync(
            actor.UserId,
            commentId,
            parsedReaction,
            actor,
            cancellationToken);
        if (error != PostsServiceError.NONE)
        {
            return ApplicationResult<CommentResponse>.Failure(ToApplicationError(error));
        }

        return ApplicationResult<CommentResponse>.Success(
            (await GetCommentResponseAsync(commentId, actor.UserId, cancellationToken))!);
    }

    public async Task<ApplicationResult<CommentResponse>> RemoveCommentReactionAsync(
        PostViewerContext actor,
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        var error = await ChangeCommentReactionAsync(
            actor.UserId,
            commentId,
            null,
            actor,
            cancellationToken);
        if (error != PostsServiceError.NONE)
        {
            return ApplicationResult<CommentResponse>.Failure(ToApplicationError(error));
        }

        return ApplicationResult<CommentResponse>.Success(
            (await GetCommentResponseAsync(commentId, actor.UserId, cancellationToken))!);
    }

    public async Task<ApplicationResult> AuthorizeMediaAccessAsync(
        PostViewerContext viewer, Guid postId, Guid mediaId, CancellationToken cancellationToken = default)
    {
        var access = await AuthorizeMediaAccessCoreAsync(viewer, postId, mediaId, cancellationToken);
        return Map(access);
    }

    private static bool TryParsePrivacy(string privacy, out PostPrivacy parsedPrivacy) =>
        EnumText.TryParse(privacy, true, out parsedPrivacy) && Enum.IsDefined(parsedPrivacy);

    private static ApplicationError InvalidPrivacy() =>
        new(
            "invalid_post_privacy",
            "Post privacy must be one of: public, friends, onlyMe.",
            ApplicationErrorType.VALIDATION);

    private static ApplicationError? ValidateContent(string? content, int maximumLength, string resource)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Trim().Length > maximumLength)
        {
            return new ApplicationError(
                $"invalid_{resource}_content",
                $"The {resource} content must contain between 1 and {maximumLength} characters.",
                ApplicationErrorType.VALIDATION);
        }

        return null;
    }

    private ApplicationError? ValidatePost(string? content, IReadOnlyList<Guid> mediaIds)
    {
        if (string.IsNullOrWhiteSpace(content) && mediaIds.Count == 0)
            return new ApplicationError("empty_post", "A post requires content or media.", ApplicationErrorType.VALIDATION);
        if ((content?.Trim().Length ?? 0) > Post.MaximumContentLength)
            return new ApplicationError("invalid_post_content",
                $"Post content cannot exceed {Post.MaximumContentLength} characters.", ApplicationErrorType.VALIDATION);
        if (mediaIds.Count > options.MaximumAttachments)
            return new ApplicationError("too_many_attachments",
                $"A post can contain at most {options.MaximumAttachments} media attachments.", ApplicationErrorType.VALIDATION);
        if (mediaIds.Count != mediaIds.Distinct().Count())
            return new ApplicationError("duplicate_attachment", "Media attachments cannot be duplicated.", ApplicationErrorType.VALIDATION);
        return null;
    }

    private static ApplicationError? ValidatePagination(int offset, int limit)
    {
        if (offset < 0 || limit < 1 || limit > MaximumLimit)
        {
            return new ApplicationError(
                ErrorCode.InvalidPagination,
                $"Offset must be non-negative and limit must be between 1 and {MaximumLimit}.",
                ApplicationErrorType.VALIDATION);
        }

        return null;
    }

    private static ApplicationResult<T> Map<T>(PostsServiceResult<T> result) =>
        result.Succeeded
            ? ApplicationResult<T>.Success(result.Value!)
            : ApplicationResult<T>.Failure(ToApplicationError(result.Error));

    private static ApplicationResult Map(PostsServiceError error) =>
        error == PostsServiceError.NONE
            ? ApplicationResult.Success()
            : ApplicationResult.Failure(ToApplicationError(error));

    private static ApplicationError ToApplicationError(PostsServiceError error) => error switch
    {
        PostsServiceError.POST_NOT_FOUND => new(
            "post_not_found", "The post was not found.", ApplicationErrorType.NOT_FOUND),
        PostsServiceError.COMMENT_NOT_FOUND => new(
            "comment_not_found", "The comment was not found.", ApplicationErrorType.NOT_FOUND),
        PostsServiceError.PARENT_COMMENT_NOT_FOUND => new(
            "parent_comment_not_found", "The parent comment was not found.", ApplicationErrorType.NOT_FOUND),
        PostsServiceError.FORBIDDEN => new(
            ErrorCode.Forbidden, "You are not allowed to perform this operation.", ApplicationErrorType.FORBIDDEN),
        PostsServiceError.RELATIONSHIP_BLOCKED => new(
            "relationship_unavailable", "This interaction is unavailable.", ApplicationErrorType.CONFLICT),
        PostsServiceError.INVALID_PARENT_COMMENT => new(
            "invalid_parent_comment", "A reply can only target a top-level comment on the same post.", ApplicationErrorType.VALIDATION),
        PostsServiceError.MEDIA_NOT_ATTACHED => new(
            "media_not_attached", "The media is not attached to this post.", ApplicationErrorType.NOT_FOUND),
        PostsServiceError.INVALID_POST_TYPE => new(
            "invalid_post_type", "Reels must be edited through the Reels experience.", ApplicationErrorType.CONFLICT),
        PostsServiceError.PROFILE_MEDIA_POST_NOT_EDITABLE => new(
            "profile_media_post_not_editable",
            "Avatar and cover update posts cannot be edited.",
            ApplicationErrorType.CONFLICT),
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, null)
    };

    public async Task<PostsServiceResult<PostResponse>> CreatePostCoreAsync(
        Guid authorUserId,
        string content,
        PostPrivacy privacy,
        IReadOnlyList<Guid> mediaIds,
        CancellationToken cancellationToken = default,
        bool addToTimelinePhotos = true,
        string? textBackground = null)
    {
        return await CreatePostInContainerCoreAsync(
            authorUserId,
            content,
            privacy,
            PostContainerType.PROFILE,
            authorUserId,
            mediaIds,
            cancellationToken,
            addToTimelinePhotos,
            textBackground);
    }

    public async Task<PostsServiceResult<PostResponse>> CreatePostInContainerCoreAsync(
        Guid authorUserId,
        string content,
        PostPrivacy privacy,
        PostContainerType containerType,
        Guid containerId,
        IReadOnlyList<Guid> mediaIds,
        CancellationToken cancellationToken = default,
        bool addToTimelinePhotos = true,
        string? textBackground = null)
    {
        var now = timeProvider.GetUtcNow();
        var post = new Post(
            Guid.NewGuid(),
            authorUserId,
            content,
            privacy,
            containerType,
            containerId,
            now);
        post.SetTextBackground(textBackground);

        dbContext.Posts.Add(post);
        for (var index = 0; index < mediaIds.Count; index++)
        {
            dbContext.PostMedia.Add(new PostMedia(post.Id, mediaIds[index], index));
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        if (addToTimelinePhotos && containerType == PostContainerType.PROFILE && post.PostType == PostType.STANDARD)
        {
            foreach (var mediaId in mediaIds)
            {
                await photosService.AddSystemMediaAsync(authorUserId, PhotoAlbumType.TIMELINE_PHOTOS, mediaId, cancellationToken);
            }
        }
        return PostsServiceResult<PostResponse>.Success(EmptySummary(post, mediaIds));
    }

    public async Task<ApplicationResult<PostResponse>> CreatePostInPageAsync(
        Guid actorUserId,
        Guid pageId,
        string content,
        IReadOnlyList<Guid> mediaIds,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidatePostRequest(content, "public", mediaIds);
        if (!validation.Succeeded)
        {
            return ApplicationResult<PostResponse>.Failure(validation.Error!);
        }

        var result = await CreatePostInContainerCoreAsync(actorUserId, content, PostPrivacy.PUBLIC,
            PostContainerType.PAGE, pageId, mediaIds, cancellationToken);
        if (!result.Succeeded)
        {
            return Map(result);
        }

        var post = await dbContext.Posts.AsNoTracking().SingleAsync(post => post.Id == result.Value!.Id, cancellationToken);
        return ApplicationResult<PostResponse>.Success(
            (await LoadResponsesAsync([post], actorUserId, cancellationToken))[0]);
    }

    public async Task<PostsServiceResult<PostResponse>> UpdatePostCoreAsync(
        Guid actorUserId,
        Guid postId,
        string content,
        PostPrivacy privacy,
        IReadOnlyList<Guid> mediaIds,
        CancellationToken cancellationToken = default)
    {
        var post = await dbContext.Posts.SingleOrDefaultAsync(
            item => item.Id == postId && item.DeletedAtUtc == null,
            cancellationToken);
        if (post is null)
        {
            return PostsServiceResult<PostResponse>.Failure(PostsServiceError.POST_NOT_FOUND);
        }

        var canManagePagePost = post.ContainerType == PostContainerType.PAGE &&
            await pagePostAccessService.CanCreatePostAsync(post.ContainerId, actorUserId, cancellationToken);
        if (post.AuthorUserId != actorUserId && !canManagePagePost)
        {
            return PostsServiceResult<PostResponse>.Failure(PostsServiceError.FORBIDDEN);
        }

        if (post.PostType != PostType.STANDARD)
        {
            return PostsServiceResult<PostResponse>.Failure(PostsServiceError.INVALID_POST_TYPE);
        }

        var isProfileMediaUpdate = Post.IsProfileMediaUpdateContent(post.Content);

        if (post.ContainerType == PostContainerType.GROUP &&
            !await groupPostAccessService.CanCreatePostAsync(
                post.ContainerId,
                actorUserId,
                cancellationToken))
        {
            return PostsServiceResult<PostResponse>.Failure(PostsServiceError.FORBIDDEN);
        }

        if (post.ContainerType == PostContainerType.PAGE)
        {
            privacy = PostPrivacy.PUBLIC;
        }

        var now = timeProvider.GetUtcNow();
        var mediaOrder = mediaIds.Select((id, index) => new { id, index })
            .ToDictionary(x => x.id, x => x.index);
        var existingMedia = await dbContext.PostMedia.Where(x => x.PostId == postId).ToListAsync(cancellationToken);
        if (isProfileMediaUpdate &&
            (!string.Equals(content, post.Content, StringComparison.Ordinal) ||
             !existingMedia.OrderBy(item => item.SortOrder).Select(item => item.MediaId).SequenceEqual(mediaIds)))
        {
            return PostsServiceResult<PostResponse>.Failure(PostsServiceError.PROFILE_MEDIA_POST_NOT_EDITABLE);
        }
        var removed = existingMedia.Where(x => !mediaIds.Contains(x.MediaId)).ToList();
        var added = mediaIds.Where(id => existingMedia.All(x => x.MediaId != id)).ToList();
        post.Update(content, privacy, now);

        foreach (var item in removed)
        {
            dbContext.PostMedia.Remove(item);
        }
        foreach (var mediaId in added)
        {
            var order = mediaOrder[mediaId];
            dbContext.PostMedia.Add(new PostMedia(post.Id, mediaId, order));
        }
        foreach (var item in existingMedia.Except(removed))
            item.ChangeSortOrder(mediaOrder[item.MediaId]);
        await dbContext.SaveChangesAsync(cancellationToken);
        return PostsServiceResult<PostResponse>.Success(
            (await LoadResponsesAsync([post], actorUserId, cancellationToken))[0]);
    }

    public async Task<PostsServiceError> DeletePostCoreAsync(
        Guid? actorUserId,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var post = await dbContext.Posts.SingleOrDefaultAsync(
            item => item.Id == postId && item.DeletedAtUtc == null,
            cancellationToken);
        if (post is null)
        {
            return PostsServiceError.POST_NOT_FOUND;
        }

        var canManagePagePost = actorUserId is not null && post.ContainerType == PostContainerType.PAGE &&
            await pagePostAccessService.CanCreatePostAsync(post.ContainerId, actorUserId.Value, cancellationToken);
        if (actorUserId is not null && post.AuthorUserId != actorUserId && !canManagePagePost)
        {
            return PostsServiceError.FORBIDDEN;
        }

        if (actorUserId is not null &&
            post.ContainerType == PostContainerType.GROUP &&
            !await groupPostAccessService.CanCreatePostAsync(
                post.ContainerId,
                actorUserId.Value,
                cancellationToken))
        {
            return PostsServiceError.FORBIDDEN;
        }

        var now = timeProvider.GetUtcNow();
        var attachments = await dbContext.PostMedia.Where(x => x.PostId == postId).ToListAsync(cancellationToken);
        post.Delete(now);

        foreach (var attachment in attachments)
        {
            dbContext.PostMedia.Remove(attachment);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return PostsServiceError.NONE;
    }

    public async Task<PostsServiceResult<PostResponse>> GetPostCoreAsync(
        PostViewerContext? viewer,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var post = await dbContext.Posts.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == postId && item.DeletedAtUtc == null,
            cancellationToken);
        if (post is null || !await CanViewPostAsync(post, viewer, cancellationToken))
        {
            return PostsServiceResult<PostResponse>.Failure(PostsServiceError.POST_NOT_FOUND);
        }

        return PostsServiceResult<PostResponse>.Success(
            (await LoadResponsesAsync([post], viewer?.UserId, cancellationToken))[0]);
    }

    public async Task<PostsServiceResult<PagedResponse<PostResponse>>> GetFeedCoreAsync(
        PostViewerContext viewer,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = VisiblePosts(viewer);
        var total = await query.CountAsync(cancellationToken);
        var posts = await query
            .OrderByDescending(post => post.CreatedAtUtc)
            .ThenByDescending(post => post.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
        var items = await LoadResponsesAsync(posts, viewer.UserId, cancellationToken);
        return PostsServiceResult<PagedResponse<PostResponse>>.Success(
            new PagedResponse<PostResponse>(items, offset, limit, total));
    }

    public async Task<PostsServiceResult<PagedResponse<PostResponse>>> GetUserPostsCoreAsync(
        PostViewerContext? viewer,
        Guid authorUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = VisiblePosts(viewer).Where(post => post.ContainerType == PostContainerType.PROFILE && post.AuthorUserId == authorUserId);
        var total = await query.CountAsync(cancellationToken);
        var posts = await query
            .OrderByDescending(post => post.IsPinned)
            .ThenByDescending(post => post.CreatedAtUtc)
            .ThenByDescending(post => post.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
        var items = await LoadResponsesAsync(posts, viewer?.UserId, cancellationToken);
        return PostsServiceResult<PagedResponse<PostResponse>>.Success(
            new PagedResponse<PostResponse>(items, offset, limit, total));
    }

    public async Task<PostsServiceResult<CommentResponse>> CreateCommentCoreAsync(
        Guid authorUserId,
        Guid postId,
        Guid? parentCommentId,
        string content,
        PostViewerContext actor,
        CancellationToken cancellationToken = default)
    {
        var post = await dbContext.Posts.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == postId && item.DeletedAtUtc == null,
            cancellationToken);
        if (post is null)
        {
            return PostsServiceResult<CommentResponse>.Failure(PostsServiceError.POST_NOT_FOUND);
        }

        var accessError = await GetInteractionAccessErrorAsync(actor, post, cancellationToken);
        if (accessError != PostsServiceError.NONE)
        {
            return PostsServiceResult<CommentResponse>.Failure(accessError);
        }

        if (parentCommentId is not null)
        {
            var parent = await dbContext.Comments.AsNoTracking().SingleOrDefaultAsync(
                comment => comment.Id == parentCommentId && comment.DeletedAtUtc == null,
                cancellationToken);
            if (parent is null)
            {
                return PostsServiceResult<CommentResponse>.Failure(PostsServiceError.PARENT_COMMENT_NOT_FOUND);
            }

            if (parent.PostId != postId || parent.ParentCommentId is not null)
            {
                return PostsServiceResult<CommentResponse>.Failure(PostsServiceError.INVALID_PARENT_COMMENT);
            }
        }

        var now = timeProvider.GetUtcNow();
        var comment = new Comment(Guid.NewGuid(), postId, authorUserId, parentCommentId, content, now);

        dbContext.Comments.Add(comment);
        var notification = await notificationService.QueueAsync(
            post.AuthorUserId,
            authorUserId,
            NotificationType.POST_COMMENT,
            NotificationEntityType.POST,
            post.Id,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (notification is not null)
        {
            await notificationService.PublishAsync(notification, cancellationToken);
        }
        return PostsServiceResult<CommentResponse>.Success(
            (await LoadCommentResponsesAsync([comment], actor.UserId, cancellationToken))[0]);
    }

    public async Task<PostsServiceResult<CommentResponse>> UpdateCommentCoreAsync(
        Guid actorUserId,
        Guid commentId,
        string content,
        CancellationToken cancellationToken = default)
    {
        var comment = await dbContext.Comments.SingleOrDefaultAsync(
            item => item.Id == commentId && item.DeletedAtUtc == null,
            cancellationToken);
        if (comment is null)
        {
            return PostsServiceResult<CommentResponse>.Failure(PostsServiceError.COMMENT_NOT_FOUND);
        }

        if (comment.AuthorUserId != actorUserId)
        {
            return PostsServiceResult<CommentResponse>.Failure(PostsServiceError.FORBIDDEN);
        }

        if (!await dbContext.Posts.AnyAsync(
                post => post.Id == comment.PostId && post.DeletedAtUtc == null,
                cancellationToken))
        {
            return PostsServiceResult<CommentResponse>.Failure(PostsServiceError.POST_NOT_FOUND);
        }

        comment.Update(content, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return PostsServiceResult<CommentResponse>.Success(
            (await LoadCommentResponsesAsync([comment], actorUserId, cancellationToken))[0]);
    }

    public async Task<PostsServiceError> DeleteCommentCoreAsync(
        Guid actorUserId,
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        var comment = await dbContext.Comments.SingleOrDefaultAsync(
            item => item.Id == commentId && item.DeletedAtUtc == null,
            cancellationToken);
        if (comment is null)
        {
            return PostsServiceError.COMMENT_NOT_FOUND;
        }

        if (comment.AuthorUserId != actorUserId &&
            !await pagePostAccessService.CanModerateCommentAsync(comment.PostId, actorUserId, cancellationToken))
        {
            return PostsServiceError.FORBIDDEN;
        }

        comment.Delete(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return PostsServiceError.NONE;
    }

    public async Task<PostsServiceResult<PagedResponse<CommentResponse>>> GetCommentsCoreAsync(
        PostViewerContext? viewer,
        Guid postId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var post = await dbContext.Posts.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == postId && item.DeletedAtUtc == null,
            cancellationToken);
        if (post is null || !await CanViewPostAsync(post, viewer, cancellationToken))
        {
            return PostsServiceResult<PagedResponse<CommentResponse>>.Failure(PostsServiceError.POST_NOT_FOUND);
        }

        var query = dbContext.Comments.AsNoTracking()
            .Where(comment => comment.PostId == postId && comment.DeletedAtUtc == null);
        var total = await query.CountAsync(cancellationToken);
        var comments = await query
            .OrderBy(comment => comment.CreatedAtUtc)
            .ThenBy(comment => comment.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return PostsServiceResult<PagedResponse<CommentResponse>>.Success(
            new PagedResponse<CommentResponse>(
                await LoadCommentResponsesAsync(comments, viewer?.UserId, cancellationToken), offset, limit, total));
    }

    public async Task<PostsServiceResult<PagedResponse<PostReactionResponse>>> GetReactionsCoreAsync(
        PostViewerContext viewer,
        Guid postId,
        ReactionType? reactionType,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var post = await dbContext.Posts.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == postId && item.DeletedAtUtc == null,
            cancellationToken);
        if (post is null || !await CanViewPostAsync(post, viewer, cancellationToken))
        {
            return PostsServiceResult<PagedResponse<PostReactionResponse>>.Failure(PostsServiceError.POST_NOT_FOUND);
        }

        var query = dbContext.PostReactions.AsNoTracking().Where(reaction => reaction.PostId == postId)
            .Where(reaction => !dbContext.BlockedUsers.AsNoTracking().Any(block =>
                (block.BlockerUserId == viewer.UserId && block.BlockedAccountId == reaction.UserId) ||
                (block.BlockedAccountId == viewer.UserId && block.BlockerUserId == reaction.UserId)));
        if (reactionType is not null)
        {
            query = query.Where(reaction => reaction.Type == reactionType.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var reactions = await query
            .OrderByDescending(reaction => reaction.UpdatedAtUtc ?? reaction.CreatedAtUtc)
            .ThenByDescending(reaction => reaction.UserId)
            .Skip(offset)
            .Take(limit)
            .Select(reaction => new ReactionListRow(reaction.UserId, reaction.Type))
            .ToListAsync(cancellationToken);
        var userIds = reactions.Select(reaction => reaction.UserId).ToArray();
        var profiles = userIds.Length == 0
            ? new Dictionary<Guid, UserReactionProfile>()
            : await dbContext.UserProfiles.AsNoTracking()
                .Where(profile => userIds.Contains(profile.UserId))
                .Select(profile => new UserReactionProfile(
                    profile.UserId,
                    profile.Username,
                    profile.DisplayName,
                    profile.AvatarMediaId == null ? profile.AvatarUrl : $"/api/users/{profile.UserId}/avatar"))
                .ToDictionaryAsync(profile => profile.UserId, cancellationToken);
        var usernames = userIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Users.AsNoTracking()
                .Where(user => userIds.Contains(user.Id))
                .ToDictionaryAsync(user => user.Id, user => user.UserName ?? user.Id.ToString(), cancellationToken);
        var pendingRequestRows = await dbContext.FriendRequests.AsNoTracking()
            .Where(request => request.Status == FriendRequestStatus.PENDING &&
                ((request.SenderUserId == viewer.UserId && userIds.Contains(request.ReceiverUserId)) ||
                 (request.ReceiverUserId == viewer.UserId && userIds.Contains(request.SenderUserId))))
            .Select(request => new { request.Id, request.SenderUserId, request.ReceiverUserId })
            .ToListAsync(cancellationToken);
        var pendingRequests = pendingRequestRows.ToDictionary(
            request => request.SenderUserId == viewer.UserId ? request.ReceiverUserId : request.SenderUserId,
            request => (Status: request.SenderUserId == viewer.UserId ? "request_sent" : "request_received", RequestId: request.Id));
        var items = reactions.Select(reaction =>
        {
            var profile = profiles.GetValueOrDefault(reaction.UserId);
            var username = profile?.Username ?? usernames.GetValueOrDefault(reaction.UserId, reaction.UserId.ToString());
            var relationship = reaction.UserId == viewer.UserId
                ? ("self", (Guid?)null)
                : viewer.FriendUserIds.Contains(reaction.UserId)
                    ? ("friends", (Guid?)null)
                    : pendingRequests.TryGetValue(reaction.UserId, out var pending)
                        ? (pending.Status, (Guid?)pending.RequestId)
                        : ("none", (Guid?)null);
            return new PostReactionResponse(
                reaction.UserId,
                username,
                profile?.DisplayName ?? username,
                profile?.AvatarUrl,
                reaction.Type.ToString().ToLowerInvariant(),
                relationship.Item1,
                relationship.Item2);
        }).ToList();

        return PostsServiceResult<PagedResponse<PostReactionResponse>>.Success(
            new PagedResponse<PostReactionResponse>(items, offset, limit, total));
    }

    public async Task<PostsServiceResult<PostResponse>> SetReactionCoreAsync(
        Guid actorUserId,
        Guid postId,
        ReactionType reactionType,
        PostViewerContext actor,
        CancellationToken cancellationToken = default)
    {
        return await ChangeReactionAsync(actorUserId, postId, reactionType, actor, cancellationToken);
    }

    public async Task<PostsServiceResult<PostResponse>> RemoveReactionCoreAsync(
        Guid actorUserId,
        Guid postId,
        PostViewerContext actor,
        CancellationToken cancellationToken = default) =>
        await ChangeReactionAsync(actorUserId, postId, null, actor, cancellationToken);

    public async Task<PostsServiceError> AuthorizeMediaAccessCoreAsync(
        PostViewerContext viewer, Guid postId, Guid mediaId, CancellationToken cancellationToken = default)
    {
        var post = await dbContext.Posts.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == postId && item.DeletedAtUtc == null,
            cancellationToken);
        if (post is null || !await CanViewPostAsync(post, viewer, cancellationToken))
        {
            return PostsServiceError.POST_NOT_FOUND;
        }
        return await dbContext.PostMedia.AnyAsync(
            x => x.PostId == postId && x.MediaId == mediaId, cancellationToken)
            ? PostsServiceError.NONE
            : PostsServiceError.MEDIA_NOT_ATTACHED;
    }

    private async Task<PostsServiceResult<PostResponse>> ChangeReactionAsync(
        Guid actorUserId,
        Guid postId,
        ReactionType? reactionType,
        PostViewerContext actor,
        CancellationToken cancellationToken)
    {
        var post = await dbContext.Posts.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == postId && item.DeletedAtUtc == null,
            cancellationToken);
        if (post is null)
        {
            return PostsServiceResult<PostResponse>.Failure(PostsServiceError.POST_NOT_FOUND);
        }

        var accessError = await GetInteractionAccessErrorAsync(actor, post, cancellationToken);
        if (accessError != PostsServiceError.NONE)
        {
            return PostsServiceResult<PostResponse>.Failure(accessError);
        }

        var now = timeProvider.GetUtcNow();
        var reaction = await dbContext.PostReactions.SingleOrDefaultAsync(
            item => item.PostId == postId && item.UserId == actorUserId,
            cancellationToken);
        if (reactionType is null)
        {
            if (reaction is not null)
            {
                dbContext.PostReactions.Remove(reaction);
            }
        }
        else if (reaction is null)
        {
            dbContext.PostReactions.Add(new PostReaction(postId, actorUserId, reactionType.Value, now));
        }
        else
        {
            reaction.ChangeTo(reactionType.Value, now);
        }

        var notification = reactionType is null
            ? null
            : await notificationService.QueueAsync(
                post.AuthorUserId,
                actorUserId,
                NotificationType.POST_REACTION,
                NotificationEntityType.POST,
                post.Id,
                cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (notification is not null)
        {
            await notificationService.PublishAsync(notification, cancellationToken);
        }
        return PostsServiceResult<PostResponse>.Success(
            (await LoadResponsesAsync([post], actorUserId, cancellationToken))[0]);
    }

    private async Task<PostsServiceError> ChangeCommentReactionAsync(
        Guid actorUserId,
        Guid commentId,
        ReactionType? reactionType,
        PostViewerContext actor,
        CancellationToken cancellationToken)
    {
        var comment = await dbContext.Comments.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == commentId && item.DeletedAtUtc == null,
            cancellationToken);
        if (comment is null)
        {
            return PostsServiceError.COMMENT_NOT_FOUND;
        }

        var post = await dbContext.Posts.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == comment.PostId && item.DeletedAtUtc == null,
            cancellationToken);
        if (post is null)
        {
            return PostsServiceError.POST_NOT_FOUND;
        }

        var accessError = await GetInteractionAccessErrorAsync(actor, post, cancellationToken);
        if (accessError != PostsServiceError.NONE)
        {
            return accessError;
        }

        var now = timeProvider.GetUtcNow();
        var reaction = await dbContext.CommentReactions.SingleOrDefaultAsync(
            item => item.CommentId == commentId && item.UserId == actorUserId,
            cancellationToken);
        if (reactionType is null)
        {
            if (reaction is not null)
            {
                dbContext.CommentReactions.Remove(reaction);
            }
        }
        else if (reaction is null)
        {
            dbContext.CommentReactions.Add(new CommentReaction(
                commentId,
                actorUserId,
                reactionType.Value,
                now));
        }
        else
        {
            reaction.ChangeTo(reactionType.Value, now);
        }

        var notification = reactionType is null
            ? null
            : await notificationService.QueueAsync(
                comment.AuthorUserId,
                actorUserId,
                NotificationType.COMMENT_REACTION,
                NotificationEntityType.COMMENT,
                comment.Id,
                cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (notification is not null)
        {
            await notificationService.PublishAsync(notification, cancellationToken);
        }

        return PostsServiceError.NONE;
    }

    private IQueryable<Post> VisiblePosts(PostViewerContext? viewer)
    {
        var source = dbContext.Posts.AsNoTracking();
        return PostVisibility.ApplyDirectAccess(source, viewer)
            .Concat(groupPostAccessService.ApplyDirectAccess(source, viewer))
            .Concat(pagePostAccessService.ApplyPublishedAccess(source))
            .Concat(eventPostAccessService.ApplyDirectAccess(source, viewer))
            .Where(post => post.PostType == PostType.STANDARD);
    }

    public async Task<bool> CanViewPostAsync(
        Post post,
        PostViewerContext? viewer,
        CancellationToken cancellationToken)
    {
        if (post.ContainerType == PostContainerType.GROUP)
        {
            return await groupPostAccessService.CanAccessPostAsync(post, viewer, cancellationToken);
        }

        if (post.ContainerType == PostContainerType.PAGE)
        {
            return await pagePostAccessService.CanAccessPostAsync(post, viewer, cancellationToken);
        }

        if (post.ContainerType == PostContainerType.EVENT)
        {
            return await eventPostAccessService.ApplyDirectAccess(dbContext.Posts.AsNoTracking(), viewer)
                .AnyAsync(item => item.Id == post.Id, cancellationToken);
        }

        return PostVisibility.CanDirectlyAccess(post, viewer);
    }

    private async Task<PostsServiceError> GetInteractionAccessErrorAsync(
        PostViewerContext actor,
        Post post,
        CancellationToken cancellationToken)
    {
        if (post.ContainerType == PostContainerType.GROUP)
        {
            if (actor.BlockedUserIds.Contains(post.AuthorUserId))
            {
                return PostsServiceError.RELATIONSHIP_BLOCKED;
            }

            return await groupPostAccessService.CanParticipateAsync(post, actor, cancellationToken)
                ? PostsServiceError.NONE
                : PostsServiceError.FORBIDDEN;
        }

        if (post.ContainerType == PostContainerType.PAGE)
        {
            return await pagePostAccessService.CanParticipateAsync(post, actor, cancellationToken)
                ? PostsServiceError.NONE
                : PostsServiceError.FORBIDDEN;
        }

        if (post.ContainerType == PostContainerType.EVENT)
        {
            var item = await dbContext.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == post.ContainerId, cancellationToken);
            return item is not null && await eventPostAccessService.CanPostAsync(item, actor.UserId, cancellationToken)
                ? PostsServiceError.NONE : PostsServiceError.FORBIDDEN;
        }

        if (actor.UserId == post.AuthorUserId)
        {
            return PostsServiceError.NONE;
        }

        if (actor.BlockedUserIds.Contains(post.AuthorUserId))
        {
            return PostsServiceError.RELATIONSHIP_BLOCKED;
        }

        if (post.Privacy == PostPrivacy.PUBLIC)
        {
            return PostsServiceError.NONE;
        }

        if (post.Privacy == PostPrivacy.FRIENDS &&
            actor.FriendUserIds.Contains(post.AuthorUserId))
        {
            return PostsServiceError.NONE;
        }

        return PostsServiceError.FORBIDDEN;
    }

    public async Task<IReadOnlyList<PostResponse>> LoadResponsesAsync(
        IReadOnlyList<Post> posts,
        Guid? viewerUserId,
        CancellationToken cancellationToken)
    {
        if (posts.Count == 0)
        {
            return [];
        }

        var postIds = posts.Select(post => post.Id).ToArray();
        var commentCounts = await dbContext.Comments.AsNoTracking()
            .Where(comment => postIds.Contains(comment.PostId) && comment.DeletedAtUtc == null)
            .GroupBy(comment => comment.PostId)
            .Select(group => new { PostId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.PostId, item => item.Count, cancellationToken);
        var shareCounts = await dbContext.PostShares.AsNoTracking()
            .Where(share => postIds.Contains(share.OriginalPostId))
            .GroupBy(share => share.OriginalPostId)
            .Select(group => new { PostId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.PostId, item => item.Count, cancellationToken);
        var reactionCounts = await dbContext.PostReactions.AsNoTracking()
            .Where(reaction => postIds.Contains(reaction.PostId))
            .GroupBy(reaction => new { reaction.PostId, reaction.Type })
            .Select(group => new { group.Key.PostId, group.Key.Type, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var viewerReactions = viewerUserId is null
            ? []
            : await dbContext.PostReactions.AsNoTracking()
                .Where(reaction => postIds.Contains(reaction.PostId) && reaction.UserId == viewerUserId.Value)
                .ToDictionaryAsync(
                    reaction => reaction.PostId,
                    reaction => reaction.Type.ToString().ToLowerInvariant(),
                    cancellationToken);
        var savedPostIds = viewerUserId is null
            ? new HashSet<Guid>()
            : await dbContext.PostSaves.AsNoTracking()
                .Where(save => postIds.Contains(save.PostId) && save.UserId == viewerUserId.Value)
                .Select(save => save.PostId)
                .ToHashSetAsync(cancellationToken);
        var attachments = await dbContext.PostMedia.AsNoTracking()
            .Where(x => postIds.Contains(x.PostId)).OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        var mentionRows = await dbContext.ContentMentions.AsNoTracking()
            .Where(mention => mention.SourceType == MentionSourceType.POST && postIds.Contains(mention.SourceId))
            .Select(mention => new MentionRow(
                mention.SourceId,
                mention.MentionedUserId,
                mention.StartIndex,
                mention.Length))
            .ToListAsync(cancellationToken);
        var mentionedUserIds = mentionRows.Select(mention => mention.UserId).Distinct().ToArray();
        var mentionedProfiles = mentionedUserIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.UserProfiles.AsNoTracking()
                .Where(profile => mentionedUserIds.Contains(profile.UserId))
                .ToDictionaryAsync(profile => profile.UserId, profile => profile.Username, cancellationToken);

        var pageIds = posts.Where(post => post.ContainerType == PostContainerType.PAGE).Select(post => post.ContainerId).Distinct().ToArray();
        var pages = pageIds.Length == 0
            ? new Dictionary<Guid, PagePostIdentity>()
            : await dbContext.Pages.AsNoTracking().Where(page => pageIds.Contains(page.Id) && page.DeletedAtUtc == null)
                .Select(page => new PagePostIdentity(page.Id, page.Username, page.Name, page.AvatarMediaId))
                .ToDictionaryAsync(page => page.Id, cancellationToken);
        var authorUserIds = posts.Where(post => post.ContainerType != PostContainerType.PAGE)
            .Select(post => post.AuthorUserId)
            .Distinct()
            .ToArray();
        var authors = authorUserIds.Length == 0
            ? new Dictionary<Guid, UserPostIdentity>()
            : await dbContext.UserProfiles.AsNoTracking()
                .Where(profile => authorUserIds.Contains(profile.UserId))
                .Select(profile => new UserPostIdentity(
                    profile.UserId,
                    profile.Username,
                    profile.DisplayName,
                    profile.AvatarMediaId == null ? profile.AvatarUrl : $"/api/users/{profile.UserId}/avatar"))
                .ToDictionaryAsync(profile => profile.UserId, cancellationToken);
        var missingAuthorUserIds = authorUserIds.Where(authorUserId => !authors.ContainsKey(authorUserId)).ToArray();
        var fallbackAuthorUsernames = missingAuthorUserIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Users.AsNoTracking()
                .Where(user => missingAuthorUserIds.Contains(user.Id))
                .ToDictionaryAsync(
                    user => user.Id,
                    user => user.UserName ?? "Người dùng",
                    cancellationToken);

        return posts.Select(post =>
        {
            var page = post.ContainerType == PostContainerType.PAGE ? pages.GetValueOrDefault(post.ContainerId) : null;
            var author = authors.GetValueOrDefault(post.AuthorUserId);
            var fallbackUsername = fallbackAuthorUsernames.GetValueOrDefault(post.AuthorUserId);
            var username = PublicProfileHandle.From(author?.Username ?? fallbackUsername ?? string.Empty);
            var displayName = PublicProfileHandle.From(author?.DisplayName ?? fallbackUsername ?? string.Empty);
            var displayAuthor = page is null
                ? new PostDisplayIdentityResponse(
                    "user",
                    post.AuthorUserId,
                    username,
                    string.IsNullOrWhiteSpace(displayName)
                        ? (string.IsNullOrWhiteSpace(username) ? "Người dùng" : username)
                        : displayName,
                    author?.AvatarUrl)
                : new PostDisplayIdentityResponse(
                    "page",
                    page.Id,
                    page.Username,
                    page.Name,
                    page.AvatarMediaId is null ? null : $"/api/pages/{page.Id}/avatar");
            return new PostResponse(
            post.Id,
            page is null ? post.AuthorUserId : null,
            post.Content,
            PrivacyName(post.Privacy),
            post.CreatedAtUtc,
            post.UpdatedAtUtc,
            attachments.Where(x => x.PostId == post.Id).Select(x => x.MediaId).ToList(),
            commentCounts.GetValueOrDefault(post.Id),
            reactionCounts
                .Where(item => item.PostId == post.Id)
                .ToDictionary(item => item.Type.ToString().ToLowerInvariant(), item => item.Count),
            viewerReactions.GetValueOrDefault(post.Id),
            displayAuthor,
            post.ContainerType.ToString().ToLowerInvariant(),
            mentionRows
                .Where(mention => mention.SourceId == post.Id && mentionedProfiles.ContainsKey(mention.UserId))
                .OrderBy(mention => mention.StartIndex)
                .Select(mention => new ContentMentionResponse(
                    mention.UserId,
                    mentionedProfiles[mention.UserId],
                    mention.StartIndex,
                    mention.Length))
                .ToList(),
            post.PostType == PostType.REEL ? "reel" : "standardPost",
            shareCounts.GetValueOrDefault(post.Id),
            post.IsPinned,
            savedPostIds.Contains(post.Id),
            post.TextBackground);
        }).ToList();
    }

    private static PostResponse EmptySummary(Post post, IReadOnlyList<Guid> mediaIds) =>
        new(
            post.Id,
            post.ContainerType == PostContainerType.PAGE ? null : post.AuthorUserId,
            post.Content,
            PrivacyName(post.Privacy),
            post.CreatedAtUtc,
            post.UpdatedAtUtc,
            mediaIds,
            0,
            new Dictionary<string, int>(),
            null,
            TextBackground: post.TextBackground);

    private sealed record PagePostIdentity(Guid Id, string Username, string Name, Guid? AvatarMediaId);

    private sealed record UserPostIdentity(Guid UserId, string Username, string DisplayName, string? AvatarUrl);

    private sealed record ReactionListRow(Guid UserId, ReactionType Type);

    private sealed record UserReactionProfile(Guid UserId, string Username, string DisplayName, string? AvatarUrl);

    private sealed record CommentAuthorProfile(Guid UserId, string Username, string DisplayName, string? AvatarUrl);

    private sealed record MentionRow(Guid SourceId, Guid UserId, int StartIndex, int Length);

    private async Task<IReadOnlyList<CommentResponse>> LoadCommentResponsesAsync(
        IReadOnlyList<Comment> comments,
        Guid? viewerUserId,
        CancellationToken cancellationToken)
    {
        if (comments.Count == 0)
        {
            return [];
        }

        var commentIds = comments.Select(comment => comment.Id).ToArray();
        var authorIds = comments.Select(comment => comment.AuthorUserId).Distinct().ToArray();
        var authors = authorIds.Length == 0
            ? new Dictionary<Guid, CommentAuthorProfile>()
            : await dbContext.UserProfiles.AsNoTracking()
                .Where(profile => authorIds.Contains(profile.UserId))
                .Select(profile => new CommentAuthorProfile(
                    profile.UserId,
                    profile.Username,
                    profile.DisplayName,
                    profile.AvatarMediaId == null ? profile.AvatarUrl : $"/api/users/{profile.UserId}/avatar"))
                .ToDictionaryAsync(profile => profile.UserId, cancellationToken);
        var missingAuthorIds = authorIds.Where(authorId => !authors.ContainsKey(authorId)).ToArray();
        var fallbackAuthorUsernames = missingAuthorIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Users.AsNoTracking()
                .Where(user => missingAuthorIds.Contains(user.Id))
                .ToDictionaryAsync(
                    user => user.Id,
                    user => user.UserName ?? "Người dùng",
                    cancellationToken);
        var mentionRows = await dbContext.ContentMentions.AsNoTracking()
            .Where(mention => mention.SourceType == MentionSourceType.COMMENT && commentIds.Contains(mention.SourceId))
            .Select(mention => new MentionRow(
                mention.SourceId,
                mention.MentionedUserId,
                mention.StartIndex,
                mention.Length))
            .ToListAsync(cancellationToken);
        var mentionedUserIds = mentionRows.Select(mention => mention.UserId).Distinct().ToArray();
        var profiles = mentionedUserIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.UserProfiles.AsNoTracking()
                .Where(profile => mentionedUserIds.Contains(profile.UserId))
                .ToDictionaryAsync(profile => profile.UserId, profile => profile.Username, cancellationToken);
        var reactionRows = await dbContext.CommentReactions.AsNoTracking()
            .Where(reaction => commentIds.Contains(reaction.CommentId))
            .Select(reaction => new { reaction.CommentId, reaction.UserId, reaction.Type })
            .ToListAsync(cancellationToken);
        var reactionCounts = reactionRows
            .GroupBy(reaction => reaction.CommentId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<string, int>)group
                    .GroupBy(reaction => reaction.Type.ToString().ToLowerInvariant())
                    .ToDictionary(reaction => reaction.Key, reaction => reaction.Count()));
        var viewerReactions = viewerUserId is null
            ? new Dictionary<Guid, string>()
            : reactionRows
                .Where(reaction => reaction.UserId == viewerUserId)
                .ToDictionary(reaction => reaction.CommentId, reaction => reaction.Type.ToString().ToLowerInvariant());

        return comments.Select(comment => new CommentResponse(
            comment.Id,
            comment.PostId,
            comment.AuthorUserId,
            comment.ParentCommentId,
            comment.Content,
            comment.CreatedAtUtc,
            comment.UpdatedAtUtc,
            reactionCounts.GetValueOrDefault(comment.Id, new Dictionary<string, int>()),
            viewerReactions.GetValueOrDefault(comment.Id),
            mentionRows
                .Where(mention => mention.SourceId == comment.Id && profiles.ContainsKey(mention.UserId))
                .OrderBy(mention => mention.StartIndex)
                .Select(mention => new ContentMentionResponse(
                    mention.UserId,
                    profiles[mention.UserId],
                    mention.StartIndex,
                    mention.Length))
                .ToList(),
            authors.TryGetValue(comment.AuthorUserId, out var author)
                ? new CommentAuthorResponse(author.UserId, author.Username, author.DisplayName, author.AvatarUrl)
                : fallbackAuthorUsernames.TryGetValue(comment.AuthorUserId, out var username)
                    ? new CommentAuthorResponse(comment.AuthorUserId, username, username, null)
                    : null)).ToList();
    }

    private static string PrivacyName(PostPrivacy privacy) => privacy switch
    {
        PostPrivacy.PUBLIC => "public",
        PostPrivacy.FRIENDS => "friends",
        PostPrivacy.ONLY_ME => "onlyMe",
        _ => throw new ArgumentOutOfRangeException(nameof(privacy), privacy, null)
    };
}

public sealed record PostViewerContext(
    Guid UserId,
    IReadOnlySet<Guid> FriendUserIds,
    IReadOnlySet<Guid> BlockedUserIds);
