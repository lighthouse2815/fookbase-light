using Fookbase.Api.Modules.Posts.DTOs;
using System.Text.Json;
using Fookbase.Api.Shared.Contracts.Posts;
using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Repositories;

internal sealed class PostsStore(
    PostsDbContext dbContext,
    TimeProvider timeProvider) : IPostsStore
{
    public async Task<PostsStoreResult<PostResponse>> CreatePostAsync(
        Guid authorUserId,
        string content,
        PostPrivacy privacy,
        IReadOnlyList<Guid> mediaIds,
        CancellationToken cancellationToken = default)
    {
        if (!await UserExistsAsync(authorUserId, cancellationToken))
        {
            return PostsStoreResult<PostResponse>.Failure(PostsStoreError.UserNotFound);
        }

        var mediaError = await ValidateMediaAsync(authorUserId, mediaIds, cancellationToken);
        if (mediaError != PostsStoreError.None)
            return PostsStoreResult<PostResponse>.Failure(mediaError);

        var now = timeProvider.GetUtcNow();
        var post = Post.Create(Guid.NewGuid(), authorUserId, content, privacy, now);
        var integrationEvent = new PostCreatedIntegrationEvent(
            Guid.NewGuid(), post.Id, post.AuthorUserId, PrivacyName(post.Privacy), post.Content, now);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Posts.Add(post);
        AddOutbox(integrationEvent.EventId, PostCreatedIntegrationEvent.EventType, integrationEvent, now);
        for (var index = 0; index < mediaIds.Count; index++)
        {
            dbContext.PostMedia.Add(PostMedia.Create(post.Id, mediaIds[index], index));
            var attached = new PostMediaAttachedIntegrationEvent(
                Guid.NewGuid(), post.Id, mediaIds[index], authorUserId, index, now);
            AddOutbox(attached.EventId, PostMediaAttachedIntegrationEvent.EventType, attached, now);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return PostsStoreResult<PostResponse>.Success(EmptySummary(post, mediaIds));
    }

    public async Task<PostsStoreResult<PostResponse>> UpdatePostAsync(
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
            return PostsStoreResult<PostResponse>.Failure(PostsStoreError.PostNotFound);
        }

        if (post.AuthorUserId != actorUserId)
        {
            return PostsStoreResult<PostResponse>.Failure(PostsStoreError.Forbidden);
        }

        var mediaError = await ValidateMediaAsync(actorUserId, mediaIds, cancellationToken);
        if (mediaError != PostsStoreError.None)
            return PostsStoreResult<PostResponse>.Failure(mediaError);

        var now = timeProvider.GetUtcNow();
        var mediaOrder = mediaIds.Select((id, index) => new { id, index })
            .ToDictionary(x => x.id, x => x.index);
        var existingMedia = await dbContext.PostMedia.Where(x => x.PostId == postId).ToListAsync(cancellationToken);
        var removed = existingMedia.Where(x => !mediaIds.Contains(x.MediaId)).ToList();
        var added = mediaIds.Where(id => existingMedia.All(x => x.MediaId != id)).ToList();
        post.Update(content, privacy, now);
        var integrationEvent = new PostUpdatedIntegrationEvent(
            Guid.NewGuid(), post.Id, post.AuthorUserId, PrivacyName(post.Privacy), post.Content, now);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        AddOutbox(integrationEvent.EventId, PostUpdatedIntegrationEvent.EventType, integrationEvent, now);
        foreach (var item in removed)
        {
            dbContext.PostMedia.Remove(item);
            var detached = new PostMediaDetachedIntegrationEvent(
                Guid.NewGuid(), post.Id, item.MediaId, actorUserId, now);
            AddOutbox(detached.EventId, PostMediaDetachedIntegrationEvent.EventType, detached, now);
        }
        foreach (var mediaId in added)
        {
            var order = mediaOrder[mediaId];
            dbContext.PostMedia.Add(PostMedia.Create(post.Id, mediaId, order));
            var attached = new PostMediaAttachedIntegrationEvent(
                Guid.NewGuid(), post.Id, mediaId, actorUserId, order, now);
            AddOutbox(attached.EventId, PostMediaAttachedIntegrationEvent.EventType, attached, now);
        }
        foreach (var item in existingMedia.Except(removed))
            item.ChangeSortOrder(mediaOrder[item.MediaId]);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return PostsStoreResult<PostResponse>.Success(
            (await LoadResponsesAsync([post], actorUserId, cancellationToken))[0]);
    }

    public async Task<PostsStoreError> DeletePostAsync(
        Guid actorUserId,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var post = await dbContext.Posts.SingleOrDefaultAsync(
            item => item.Id == postId && item.DeletedAtUtc == null,
            cancellationToken);
        if (post is null)
        {
            return PostsStoreError.PostNotFound;
        }

        if (post.AuthorUserId != actorUserId)
        {
            return PostsStoreError.Forbidden;
        }

        var now = timeProvider.GetUtcNow();
        var attachments = await dbContext.PostMedia.Where(x => x.PostId == postId).ToListAsync(cancellationToken);
        post.Delete(now);
        var integrationEvent = new PostDeletedIntegrationEvent(
            Guid.NewGuid(), post.Id, post.AuthorUserId, now);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        AddOutbox(integrationEvent.EventId, PostDeletedIntegrationEvent.EventType, integrationEvent, now);
        foreach (var attachment in attachments)
        {
            dbContext.PostMedia.Remove(attachment);
            var detached = new PostMediaDetachedIntegrationEvent(
                Guid.NewGuid(), post.Id, attachment.MediaId, actorUserId, now);
            AddOutbox(detached.EventId, PostMediaDetachedIntegrationEvent.EventType, detached, now);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return PostsStoreError.None;
    }

    public async Task<PostsStoreResult<PostResponse>> GetPostAsync(
        Guid? viewerUserId,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var post = await VisiblePosts(viewerUserId)
            .SingleOrDefaultAsync(item => item.Id == postId, cancellationToken);
        if (post is null)
        {
            return PostsStoreResult<PostResponse>.Failure(PostsStoreError.PostNotFound);
        }

        return PostsStoreResult<PostResponse>.Success(
            (await LoadResponsesAsync([post], viewerUserId, cancellationToken))[0]);
    }

    public async Task<PostsStoreResult<PagedResponse<PostResponse>>> GetFeedAsync(
        Guid viewerUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!await UserExistsAsync(viewerUserId, cancellationToken))
        {
            return PostsStoreResult<PagedResponse<PostResponse>>.Failure(PostsStoreError.UserNotFound);
        }

        var query = VisiblePosts(viewerUserId);
        var total = await query.CountAsync(cancellationToken);
        var posts = await query
            .OrderByDescending(post => post.CreatedAtUtc)
            .ThenByDescending(post => post.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
        var items = await LoadResponsesAsync(posts, viewerUserId, cancellationToken);
        return PostsStoreResult<PagedResponse<PostResponse>>.Success(
            new PagedResponse<PostResponse>(items, offset, limit, total));
    }

    public async Task<PostsStoreResult<PagedResponse<PostResponse>>> GetUserPostsAsync(
        Guid? viewerUserId,
        Guid authorUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!await UserExistsAsync(authorUserId, cancellationToken))
        {
            return PostsStoreResult<PagedResponse<PostResponse>>.Failure(PostsStoreError.UserNotFound);
        }

        var query = VisiblePosts(viewerUserId).Where(post => post.AuthorUserId == authorUserId);
        var total = await query.CountAsync(cancellationToken);
        var posts = await query
            .OrderByDescending(post => post.CreatedAtUtc)
            .ThenByDescending(post => post.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
        var items = await LoadResponsesAsync(posts, viewerUserId, cancellationToken);
        return PostsStoreResult<PagedResponse<PostResponse>>.Success(
            new PagedResponse<PostResponse>(items, offset, limit, total));
    }

    public async Task<PostsStoreResult<CommentResponse>> CreateCommentAsync(
        Guid authorUserId,
        Guid postId,
        Guid? parentCommentId,
        string content,
        CancellationToken cancellationToken = default)
    {
        if (!await UserExistsAsync(authorUserId, cancellationToken))
        {
            return PostsStoreResult<CommentResponse>.Failure(PostsStoreError.UserNotFound);
        }

        var post = await dbContext.Posts.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == postId && item.DeletedAtUtc == null,
            cancellationToken);
        if (post is null)
        {
            return PostsStoreResult<CommentResponse>.Failure(PostsStoreError.PostNotFound);
        }

        var accessError = await GetInteractionAccessErrorAsync(authorUserId, post, cancellationToken);
        if (accessError != PostsStoreError.None)
        {
            return PostsStoreResult<CommentResponse>.Failure(accessError);
        }

        if (parentCommentId is not null)
        {
            var parent = await dbContext.Comments.AsNoTracking().SingleOrDefaultAsync(
                comment => comment.Id == parentCommentId && comment.DeletedAtUtc == null,
                cancellationToken);
            if (parent is null)
            {
                return PostsStoreResult<CommentResponse>.Failure(PostsStoreError.ParentCommentNotFound);
            }

            if (parent.PostId != postId || parent.ParentCommentId is not null)
            {
                return PostsStoreResult<CommentResponse>.Failure(PostsStoreError.InvalidParentComment);
            }
        }

        var now = timeProvider.GetUtcNow();
        var comment = Comment.Create(Guid.NewGuid(), postId, authorUserId, parentCommentId, content, now);
        var integrationEvent = new CommentCreatedIntegrationEvent(
            Guid.NewGuid(), comment.Id, post.Id, post.AuthorUserId, authorUserId, parentCommentId, now);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Comments.Add(comment);
        AddOutbox(integrationEvent.EventId, CommentCreatedIntegrationEvent.EventType, integrationEvent, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return PostsStoreResult<CommentResponse>.Success(ToResponse(comment));
    }

    public async Task<PostsStoreResult<CommentResponse>> UpdateCommentAsync(
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
            return PostsStoreResult<CommentResponse>.Failure(PostsStoreError.CommentNotFound);
        }

        if (comment.AuthorUserId != actorUserId)
        {
            return PostsStoreResult<CommentResponse>.Failure(PostsStoreError.Forbidden);
        }

        if (!await dbContext.Posts.AnyAsync(
                post => post.Id == comment.PostId && post.DeletedAtUtc == null,
                cancellationToken))
        {
            return PostsStoreResult<CommentResponse>.Failure(PostsStoreError.PostNotFound);
        }

        comment.Update(content, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return PostsStoreResult<CommentResponse>.Success(ToResponse(comment));
    }

    public async Task<PostsStoreError> DeleteCommentAsync(
        Guid actorUserId,
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        var comment = await dbContext.Comments.SingleOrDefaultAsync(
            item => item.Id == commentId && item.DeletedAtUtc == null,
            cancellationToken);
        if (comment is null)
        {
            return PostsStoreError.CommentNotFound;
        }

        if (comment.AuthorUserId != actorUserId)
        {
            return PostsStoreError.Forbidden;
        }

        comment.Delete(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return PostsStoreError.None;
    }

    public async Task<PostsStoreResult<PagedResponse<CommentResponse>>> GetCommentsAsync(
        Guid? viewerUserId,
        Guid postId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!await VisiblePosts(viewerUserId).AnyAsync(post => post.Id == postId, cancellationToken))
        {
            return PostsStoreResult<PagedResponse<CommentResponse>>.Failure(PostsStoreError.PostNotFound);
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
        return PostsStoreResult<PagedResponse<CommentResponse>>.Success(
            new PagedResponse<CommentResponse>(comments.Select(ToResponse).ToList(), offset, limit, total));
    }

    public async Task<PostsStoreResult<PostResponse>> SetReactionAsync(
        Guid actorUserId,
        Guid postId,
        ReactionType reactionType,
        CancellationToken cancellationToken = default)
    {
        return await ChangeReactionAsync(actorUserId, postId, reactionType, cancellationToken);
    }

    public async Task<PostsStoreResult<PostResponse>> RemoveReactionAsync(
        Guid actorUserId,
        Guid postId,
        CancellationToken cancellationToken = default) =>
        await ChangeReactionAsync(actorUserId, postId, null, cancellationToken);

    public async Task<PostsStoreError> AuthorizeMediaAccessAsync(
        Guid viewerUserId, Guid postId, Guid mediaId, CancellationToken cancellationToken = default)
    {
        var post = await VisiblePosts(viewerUserId)
            .SingleOrDefaultAsync(x => x.Id == postId, cancellationToken);
        if (post is null) return PostsStoreError.PostNotFound;
        return await dbContext.PostMedia.AnyAsync(
            x => x.PostId == postId && x.MediaId == mediaId, cancellationToken)
            ? PostsStoreError.None
            : PostsStoreError.MediaNotAttached;
    }

    private async Task<PostsStoreResult<PostResponse>> ChangeReactionAsync(
        Guid actorUserId,
        Guid postId,
        ReactionType? reactionType,
        CancellationToken cancellationToken)
    {
        if (!await UserExistsAsync(actorUserId, cancellationToken))
        {
            return PostsStoreResult<PostResponse>.Failure(PostsStoreError.UserNotFound);
        }

        var post = await dbContext.Posts.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == postId && item.DeletedAtUtc == null,
            cancellationToken);
        if (post is null)
        {
            return PostsStoreResult<PostResponse>.Failure(PostsStoreError.PostNotFound);
        }

        var accessError = await GetInteractionAccessErrorAsync(actorUserId, post, cancellationToken);
        if (accessError != PostsStoreError.None)
        {
            return PostsStoreResult<PostResponse>.Failure(accessError);
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

        var integrationEvent = new PostReactionChangedIntegrationEvent(
            Guid.NewGuid(), post.Id, post.AuthorUserId, actorUserId,
            reactionType?.ToString().ToLowerInvariant(), now);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        AddOutbox(
            integrationEvent.EventId,
            PostReactionChangedIntegrationEvent.EventType,
            integrationEvent,
            now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return PostsStoreResult<PostResponse>.Success(
            (await LoadResponsesAsync([post], actorUserId, cancellationToken))[0]);
    }

    private IQueryable<Post> VisiblePosts(Guid? viewerUserId)
    {
        var query = dbContext.Posts.AsNoTracking().Where(post => post.DeletedAtUtc == null);
        if (viewerUserId is null)
        {
            return query.Where(post => post.Privacy == PostPrivacy.Public);
        }

        var viewer = viewerUserId.Value;
        return query.Where(post =>
            post.AuthorUserId == viewer ||
            (!dbContext.BlockedEdges.Any(edge =>
                    edge.IsActive &&
                    ((edge.BlockerUserId == viewer && edge.BlockedUserId == post.AuthorUserId) ||
                     (edge.BlockerUserId == post.AuthorUserId && edge.BlockedUserId == viewer))) &&
             (post.Privacy == PostPrivacy.Public ||
              (post.Privacy == PostPrivacy.Friends && dbContext.FriendEdges.Any(edge =>
                  edge.IsActive &&
                  ((edge.UserId1 == viewer && edge.UserId2 == post.AuthorUserId) ||
                   (edge.UserId1 == post.AuthorUserId && edge.UserId2 == viewer)))))));
    }

    private async Task<PostsStoreError> GetInteractionAccessErrorAsync(
        Guid actorUserId,
        Post post,
        CancellationToken cancellationToken)
    {
        if (actorUserId == post.AuthorUserId)
        {
            return PostsStoreError.None;
        }

        if (await IsBlockedAsync(actorUserId, post.AuthorUserId, cancellationToken))
        {
            return PostsStoreError.RelationshipBlocked;
        }

        if (post.Privacy == PostPrivacy.Public)
        {
            return PostsStoreError.None;
        }

        if (post.Privacy == PostPrivacy.Friends &&
            await AreFriendsAsync(actorUserId, post.AuthorUserId, cancellationToken))
        {
            return PostsStoreError.None;
        }

        return PostsStoreError.Forbidden;
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

    private Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.KnownUsers.AnyAsync(user => user.UserId == userId, cancellationToken);

    private async Task<PostsStoreError> ValidateMediaAsync(
        Guid ownerUserId, IReadOnlyList<Guid> mediaIds, CancellationToken cancellationToken)
    {
        if (mediaIds.Count == 0) return PostsStoreError.None;
        var media = await dbContext.KnownMedia.AsNoTracking()
            .Where(x => mediaIds.Contains(x.MediaId)).ToListAsync(cancellationToken);
        if (media.Count != mediaIds.Count || media.Any(x => !x.IsReady))
            return PostsStoreError.InvalidMedia;
        return media.Any(x => x.OwnerUserId != ownerUserId)
            ? PostsStoreError.MediaNotOwned
            : PostsStoreError.None;
    }

    private Task<bool> IsBlockedAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken) =>
        dbContext.BlockedEdges.AnyAsync(edge =>
            edge.IsActive &&
            ((edge.BlockerUserId == firstUserId && edge.BlockedUserId == secondUserId) ||
             (edge.BlockerUserId == secondUserId && edge.BlockedUserId == firstUserId)),
            cancellationToken);

    private Task<bool> AreFriendsAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken) =>
        dbContext.FriendEdges.AnyAsync(edge =>
            edge.IsActive &&
            ((edge.UserId1 == firstUserId && edge.UserId2 == secondUserId) ||
             (edge.UserId1 == secondUserId && edge.UserId2 == firstUserId)),
            cancellationToken);

    private void AddOutbox(
        Guid eventId,
        string eventType,
        object integrationEvent,
        DateTimeOffset occurredAtUtc) =>
        dbContext.OutboxMessages.Add(OutboxMessage.Create(
            eventId,
            eventType,
            JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType()),
            occurredAtUtc));

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
