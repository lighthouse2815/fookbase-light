using Fookbase.Api.Modules.Posts.Common;
using Fookbase.Api.Modules.Posts.Config;
using Fookbase.Api.Modules.Posts.Data;
using Fookbase.Api.Modules.Posts.DTOs.Responses;
using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Services;

public sealed class PostsService(
    PostsDbContext dbContext,
    TimeProvider timeProvider,
    PostsOptions options)
{
    private const int MaximumLimit = 100;

    public async Task<ApplicationResult<PostResponse>> CreatePostAsync(
        Guid actorUserId,
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

        return Map(await CreatePostCoreAsync(
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
            return ApplicationResult.Failure(ToApplicationError(PostsServiceError.PostNotFound));
        }

        return post.AuthorUserId == actorUserId
            ? ApplicationResult.Success()
            : ApplicationResult.Failure(ToApplicationError(PostsServiceError.Forbidden));
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
                ApplicationErrorType.Validation));
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

    public async Task<ApplicationResult> AuthorizeMediaAccessAsync(
        PostViewerContext viewer, Guid postId, Guid mediaId, CancellationToken cancellationToken = default)
    {
        var access = await AuthorizeMediaAccessCoreAsync(viewer, postId, mediaId, cancellationToken);
        return Map(access);
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

    private static ApplicationResult<T> Map<T>(PostsServiceResult<T> result) =>
        result.Succeeded
            ? ApplicationResult<T>.Success(result.Value!)
            : ApplicationResult<T>.Failure(ToApplicationError(result.Error));

    private static ApplicationResult Map(PostsServiceError error) =>
        error == PostsServiceError.None
            ? ApplicationResult.Success()
            : ApplicationResult.Failure(ToApplicationError(error));

    private static ApplicationError ToApplicationError(PostsServiceError error) => error switch
    {
        PostsServiceError.PostNotFound => new(
            "post_not_found", "The post was not found.", ApplicationErrorType.NotFound),
        PostsServiceError.CommentNotFound => new(
            "comment_not_found", "The comment was not found.", ApplicationErrorType.NotFound),
        PostsServiceError.ParentCommentNotFound => new(
            "parent_comment_not_found", "The parent comment was not found.", ApplicationErrorType.NotFound),
        PostsServiceError.Forbidden => new(
            "forbidden", "You are not allowed to perform this operation.", ApplicationErrorType.Forbidden),
        PostsServiceError.RelationshipBlocked => new(
            "relationship_unavailable", "This interaction is unavailable.", ApplicationErrorType.Conflict),
        PostsServiceError.InvalidParentComment => new(
            "invalid_parent_comment", "A reply can only target a top-level comment on the same post.", ApplicationErrorType.Validation),
        PostsServiceError.MediaNotAttached => new(
            "media_not_attached", "The media is not attached to this post.", ApplicationErrorType.NotFound),
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, null)
    };

    public async Task<PostsServiceResult<PostResponse>> CreatePostCoreAsync(
        Guid authorUserId,
        string content,
        PostPrivacy privacy,
        IReadOnlyList<Guid> mediaIds,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var post = Post.Create(Guid.NewGuid(), authorUserId, content, privacy, now);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Posts.Add(post);
        for (var index = 0; index < mediaIds.Count; index++)
        {
            dbContext.PostMedia.Add(PostMedia.Create(post.Id, mediaIds[index], index));
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return PostsServiceResult<PostResponse>.Success(EmptySummary(post, mediaIds));
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
            return PostsServiceResult<PostResponse>.Failure(PostsServiceError.PostNotFound);
        }

        if (post.AuthorUserId != actorUserId)
        {
            return PostsServiceResult<PostResponse>.Failure(PostsServiceError.Forbidden);
        }

        var now = timeProvider.GetUtcNow();
        var mediaOrder = mediaIds.Select((id, index) => new { id, index })
            .ToDictionary(x => x.id, x => x.index);
        var existingMedia = await dbContext.PostMedia.Where(x => x.PostId == postId).ToListAsync(cancellationToken);
        var removed = existingMedia.Where(x => !mediaIds.Contains(x.MediaId)).ToList();
        var added = mediaIds.Where(id => existingMedia.All(x => x.MediaId != id)).ToList();
        post.Update(content, privacy, now);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (var item in removed)
        {
            dbContext.PostMedia.Remove(item);
        }
        foreach (var mediaId in added)
        {
            var order = mediaOrder[mediaId];
            dbContext.PostMedia.Add(PostMedia.Create(post.Id, mediaId, order));
        }
        foreach (var item in existingMedia.Except(removed))
            item.ChangeSortOrder(mediaOrder[item.MediaId]);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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
            return PostsServiceError.PostNotFound;
        }

        if (actorUserId is not null && post.AuthorUserId != actorUserId)
        {
            return PostsServiceError.Forbidden;
        }

        var now = timeProvider.GetUtcNow();
        var attachments = await dbContext.PostMedia.Where(x => x.PostId == postId).ToListAsync(cancellationToken);
        post.Delete(now);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (var attachment in attachments)
        {
            dbContext.PostMedia.Remove(attachment);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return PostsServiceError.None;
    }

    public async Task<PostsServiceResult<PostResponse>> GetPostCoreAsync(
        PostViewerContext? viewer,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var post = await VisiblePosts(viewer)
            .SingleOrDefaultAsync(item => item.Id == postId, cancellationToken);
        if (post is null)
        {
            return PostsServiceResult<PostResponse>.Failure(PostsServiceError.PostNotFound);
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
        var query = VisiblePosts(viewer).Where(post => post.AuthorUserId == authorUserId);
        var total = await query.CountAsync(cancellationToken);
        var posts = await query
            .OrderByDescending(post => post.CreatedAtUtc)
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
            return PostsServiceResult<CommentResponse>.Failure(PostsServiceError.PostNotFound);
        }

        var accessError = GetInteractionAccessError(actor, post);
        if (accessError != PostsServiceError.None)
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
                return PostsServiceResult<CommentResponse>.Failure(PostsServiceError.ParentCommentNotFound);
            }

            if (parent.PostId != postId || parent.ParentCommentId is not null)
            {
                return PostsServiceResult<CommentResponse>.Failure(PostsServiceError.InvalidParentComment);
            }
        }

        var now = timeProvider.GetUtcNow();
        var comment = Comment.Create(Guid.NewGuid(), postId, authorUserId, parentCommentId, content, now);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Comments.Add(comment);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return PostsServiceResult<CommentResponse>.Success(ToResponse(comment));
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
            return PostsServiceResult<CommentResponse>.Failure(PostsServiceError.CommentNotFound);
        }

        if (comment.AuthorUserId != actorUserId)
        {
            return PostsServiceResult<CommentResponse>.Failure(PostsServiceError.Forbidden);
        }

        if (!await dbContext.Posts.AnyAsync(
                post => post.Id == comment.PostId && post.DeletedAtUtc == null,
                cancellationToken))
        {
            return PostsServiceResult<CommentResponse>.Failure(PostsServiceError.PostNotFound);
        }

        comment.Update(content, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return PostsServiceResult<CommentResponse>.Success(ToResponse(comment));
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
            return PostsServiceError.CommentNotFound;
        }

        if (comment.AuthorUserId != actorUserId)
        {
            return PostsServiceError.Forbidden;
        }

        comment.Delete(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return PostsServiceError.None;
    }

    public async Task<PostsServiceResult<PagedResponse<CommentResponse>>> GetCommentsCoreAsync(
        PostViewerContext? viewer,
        Guid postId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!await VisiblePosts(viewer).AnyAsync(post => post.Id == postId, cancellationToken))
        {
            return PostsServiceResult<PagedResponse<CommentResponse>>.Failure(PostsServiceError.PostNotFound);
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
            new PagedResponse<CommentResponse>(comments.Select(ToResponse).ToList(), offset, limit, total));
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
        var post = await VisiblePosts(viewer)
            .SingleOrDefaultAsync(x => x.Id == postId, cancellationToken);
        if (post is null) return PostsServiceError.PostNotFound;
        return await dbContext.PostMedia.AnyAsync(
            x => x.PostId == postId && x.MediaId == mediaId, cancellationToken)
            ? PostsServiceError.None
            : PostsServiceError.MediaNotAttached;
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
            return PostsServiceResult<PostResponse>.Failure(PostsServiceError.PostNotFound);
        }

        var accessError = GetInteractionAccessError(actor, post);
        if (accessError != PostsServiceError.None)
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
            dbContext.PostReactions.Add(PostReaction.Create(postId, actorUserId, reactionType.Value, now));
        }
        else
        {
            reaction.ChangeTo(reactionType.Value, now);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return PostsServiceResult<PostResponse>.Success(
            (await LoadResponsesAsync([post], actorUserId, cancellationToken))[0]);
    }

    private IQueryable<Post> VisiblePosts(PostViewerContext? viewer)
    {
        var query = dbContext.Posts.AsNoTracking().Where(post => post.DeletedAtUtc == null);
        if (viewer is null)
        {
            return query.Where(post => post.Privacy == PostPrivacy.Public);
        }

        var viewerUserId = viewer.UserId;
        var friendUserIds = viewer.FriendUserIds;
        var blockedUserIds = viewer.BlockedUserIds;
        return query.Where(post =>
            post.AuthorUserId == viewerUserId ||
            (!blockedUserIds.Contains(post.AuthorUserId) &&
             (post.Privacy == PostPrivacy.Public ||
              (post.Privacy == PostPrivacy.Friends && friendUserIds.Contains(post.AuthorUserId)))));
    }

    private static PostsServiceError GetInteractionAccessError(
        PostViewerContext actor,
        Post post)
    {
        if (actor.UserId == post.AuthorUserId)
        {
            return PostsServiceError.None;
        }

        if (actor.BlockedUserIds.Contains(post.AuthorUserId))
        {
            return PostsServiceError.RelationshipBlocked;
        }

        if (post.Privacy == PostPrivacy.Public)
        {
            return PostsServiceError.None;
        }

        if (post.Privacy == PostPrivacy.Friends &&
            actor.FriendUserIds.Contains(post.AuthorUserId))
        {
            return PostsServiceError.None;
        }

        return PostsServiceError.Forbidden;
    }

    private async Task<IReadOnlyList<PostResponse>> LoadResponsesAsync(
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
        var attachments = await dbContext.PostMedia.AsNoTracking()
            .Where(x => postIds.Contains(x.PostId)).OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        return posts.Select(post => new PostResponse(
            post.Id,
            post.AuthorUserId,
            post.Content,
            PrivacyName(post.Privacy),
            post.CreatedAtUtc,
            post.UpdatedAtUtc,
            attachments.Where(x => x.PostId == post.Id).Select(x => x.MediaId).ToList(),
            commentCounts.GetValueOrDefault(post.Id),
            reactionCounts
                .Where(item => item.PostId == post.Id)
                .ToDictionary(item => item.Type.ToString().ToLowerInvariant(), item => item.Count),
            viewerReactions.GetValueOrDefault(post.Id))).ToList();
    }

    private static PostResponse EmptySummary(Post post, IReadOnlyList<Guid> mediaIds) =>
        new(
            post.Id,
            post.AuthorUserId,
            post.Content,
            PrivacyName(post.Privacy),
            post.CreatedAtUtc,
            post.UpdatedAtUtc,
            mediaIds,
            0,
            new Dictionary<string, int>(),
            null);

    private static CommentResponse ToResponse(Comment comment) =>
        new(
            comment.Id,
            comment.PostId,
            comment.AuthorUserId,
            comment.ParentCommentId,
            comment.Content,
            comment.CreatedAtUtc,
            comment.UpdatedAtUtc);

    private static string PrivacyName(PostPrivacy privacy) => privacy switch
    {
        PostPrivacy.Public => "public",
        PostPrivacy.Friends => "friends",
        PostPrivacy.OnlyMe => "onlyMe",
        _ => throw new ArgumentOutOfRangeException(nameof(privacy), privacy, null)
    };
}

public sealed record PostViewerContext(
    Guid UserId,
    IReadOnlySet<Guid> FriendUserIds,
    IReadOnlySet<Guid> BlockedUserIds);
