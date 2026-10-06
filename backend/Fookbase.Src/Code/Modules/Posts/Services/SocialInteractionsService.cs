using Fookbase.Api.Modules.Notifications.Domain.Enums;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Groups.Services;
using Fookbase.Api.Modules.Notifications.Entities;
using Fookbase.Api.Modules.Notifications.Services;
using Fookbase.Api.Modules.Pages.Services;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Services;

public sealed class SocialInteractionsService(
    FookbaseDbContext dbContext,
    FriendsService friendsService,
    GroupPostAccessService groupPostAccessService,
    PagePostAccessService pagePostAccessService,
    NotificationService notificationService,
    IDataProtectionProvider protectionProvider,
    TimeProvider timeProvider)
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 50;
    public const int MaximumMentionsPerContent = 20;
    public const int MaximumHashtagsPerPost = 20;

    private const int CursorVersion = 1;
    private static readonly Regex MentionPattern = new(
        @"(?<![\p{L}\p{N}_.-])@(?<username>[A-Za-z0-9_.-]{3,32})(?![A-Za-z0-9_.-])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex HashtagPattern = new(
        @"(?<![\p{L}\p{N}_])#(?<tag>[\p{L}\p{N}_]{1,50})(?![\p{L}\p{N}_])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<ApplicationResult> SaveAsync(
        Guid actorUserId,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var viewer = await CreateViewerContextAsync(actorUserId, cancellationToken);
        var post = await dbContext.Posts.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == postId && item.DeletedAtUtc == null,
            cancellationToken);
        if (post is null || !await CanViewPostAsync(post, viewer, cancellationToken))
        {
            return PostNotFound();
        }

        var alreadySaved = await dbContext.PostSaves.AnyAsync(
            save => save.UserId == actorUserId && save.PostId == postId,
            cancellationToken);
        if (alreadySaved)
        {
            return ApplicationResult.Success();
        }

        dbContext.PostSaves.Add(new PostSave(actorUserId, postId, timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult> RemoveSaveAsync(
        Guid actorUserId,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var save = await dbContext.PostSaves.SingleOrDefaultAsync(
            item => item.UserId == actorUserId && item.PostId == postId,
            cancellationToken);
        if (save is not null)
        {
            dbContext.PostSaves.Remove(save);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult<AccessiblePostsPage>> GetSavedPostsAsync(
        Guid actorUserId,
        string? cursorValue,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaximumPageSize)
        {
            return Validation<AccessiblePostsPage>(
                "invalid_saved_limit", $"Limit must be between 1 and {MaximumPageSize}.");
        }

        SavedCursor? cursor;
        try
        {
            cursor = DecodeCursor<SavedCursor>(cursorValue, actorUserId, "saved");
        }
        catch (FormatException)
        {
            return Validation<AccessiblePostsPage>("invalid_saved_cursor", "The saved cursor is invalid.");
        }

        var viewer = await CreateViewerContextAsync(actorUserId, cancellationToken);
        var visible = new List<Post>(limit + 1);
        var scanCursor = cursor;
        while (visible.Count <= limit)
        {
            var query = from save in dbContext.PostSaves.AsNoTracking()
                        join post in dbContext.Posts.AsNoTracking() on save.PostId equals post.Id
                        where save.UserId == actorUserId && post.DeletedAtUtc == null
                        select new
                        {
                            save.SavedAtUtc,
                            save.PostId,
                            Post = post
                        };
            if (scanCursor is not null)
            {
                query = query.Where(item => item.SavedAtUtc < scanCursor.SavedAtUtc ||
                    item.SavedAtUtc == scanCursor.SavedAtUtc && item.PostId.CompareTo(scanCursor.PostId) < 0);
            }

            var candidates = await query
                .OrderByDescending(item => item.SavedAtUtc)
                .ThenByDescending(item => item.PostId)
                .Take(MaximumPageSize * 2)
                .ToListAsync(cancellationToken);
            if (candidates.Count == 0)
            {
                break;
            }

            foreach (var candidate in candidates)
            {
                scanCursor = new SavedCursor(candidate.SavedAtUtc, candidate.PostId);
                if (await CanViewPostAsync(candidate.Post, viewer, cancellationToken))
                {
                    visible.Add(candidate.Post);
                    if (visible.Count > limit)
                    {
                        break;
                    }
                }
            }

            if (visible.Count > limit || candidates.Count < MaximumPageSize * 2)
            {
                break;
            }
        }

        var items = visible.Take(limit).ToList();
        var nextCursor = visible.Count > limit
            ? EncodeCursor(new SavedCursor(
                await SavedAtUtcForAsync(actorUserId, items[^1].Id, cancellationToken),
                items[^1].Id), actorUserId, "saved")
            : null;
        return ApplicationResult<AccessiblePostsPage>.Success(new(items, nextCursor));
    }

    public async Task<ApplicationResult<CreatedPostShare>> ShareAsync(
        Guid actorUserId,
        Guid originalPostId,
        string? destinationTypeValue,
        Guid destinationId,
        string? caption,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<PostShareDestinationType>(destinationTypeValue, true, out var destinationType) ||
            !Enum.IsDefined(destinationType) || destinationId == Guid.Empty)
        {
            return Validation<CreatedPostShare>(
                "invalid_share_destination", "A valid profile, group, or page destination is required.");
        }

        if (caption?.Trim().Length > PostShare.MaximumCaptionLength)
        {
            return Validation<CreatedPostShare>(
                "invalid_share_caption", $"Share caption cannot exceed {PostShare.MaximumCaptionLength} characters.");
        }

        var viewer = await CreateViewerContextAsync(actorUserId, cancellationToken);
        var original = await dbContext.Posts.AsNoTracking().SingleOrDefaultAsync(
            post => post.Id == originalPostId && post.DeletedAtUtc == null,
            cancellationToken);
        if (original is null || !await CanViewPostAsync(original, viewer, cancellationToken))
        {
            return PostNotFound<CreatedPostShare>();
        }

        var destinationAllowed = destinationType switch
        {
            PostShareDestinationType.PROFILE => destinationId == actorUserId,
            PostShareDestinationType.GROUP => await groupPostAccessService.CanCreatePostAsync(
                destinationId, actorUserId, cancellationToken),
            PostShareDestinationType.PAGE => await pagePostAccessService.CanCreatePostAsync(
                destinationId, actorUserId, cancellationToken),
            _ => false
        };
        if (!destinationAllowed)
        {
            return ApplicationResult<CreatedPostShare>.Failure(new ApplicationError(
                "share_destination_forbidden",
                "You are not allowed to share to this destination.",
                ApplicationErrorType.FORBIDDEN));
        }

        var share = new PostShare(
            Guid.NewGuid(),
            originalPostId,
            actorUserId,
            destinationType,
            destinationId,
            caption,
            timeProvider.GetUtcNow());
        dbContext.PostShares.Add(share);
        var notification = await notificationService.QueueAsync(
            original.AuthorUserId,
            actorUserId,
            NotificationType.POST_SHARED,
            NotificationEntityType.POST,
            original.Id,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (notification is not null)
        {
            await notificationService.PublishAsync(notification, cancellationToken);
        }

        return ApplicationResult<CreatedPostShare>.Success(new(share, original));
    }

    public async Task<ApplicationResult<AccessiblePostsPage>> GetHashtagPostsAsync(
        Guid? viewerUserId,
        string tagValue,
        string? cursorValue,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaximumPageSize)
        {
            return Validation<AccessiblePostsPage>(
                "invalid_hashtag_limit", $"Limit must be between 1 and {MaximumPageSize}.");
        }

        if (!TryNormalizeHashtag(tagValue, out var tag))
        {
            return Validation<AccessiblePostsPage>("invalid_hashtag", "The hashtag is invalid.");
        }

        HashtagCursor? cursor;
        try
        {
            cursor = DecodeCursor<HashtagCursor>(cursorValue, viewerUserId, $"hashtag:{tag}");
        }
        catch (FormatException)
        {
            return Validation<AccessiblePostsPage>("invalid_hashtag_cursor", "The hashtag cursor is invalid.");
        }

        var viewer = await CreateOptionalViewerContextAsync(viewerUserId, cancellationToken);
        var visible = new List<Post>(limit + 1);
        var scanCursor = cursor;
        while (visible.Count <= limit)
        {
            var query = from postHashtag in dbContext.PostHashtags.AsNoTracking()
                        join hashtag in dbContext.Hashtags.AsNoTracking() on postHashtag.HashtagId equals hashtag.Id
                        join post in dbContext.Posts.AsNoTracking() on postHashtag.PostId equals post.Id
                        where hashtag.NormalizedName == tag && post.DeletedAtUtc == null &&
                              (post.PostType == PostType.STANDARD || post.PostType == PostType.REEL)
                        select post;
            if (scanCursor is not null)
            {
                query = query.Where(post => post.CreatedAtUtc < scanCursor.CreatedAtUtc ||
                    post.CreatedAtUtc == scanCursor.CreatedAtUtc && post.Id.CompareTo(scanCursor.PostId) < 0);
            }

            var candidates = await query
                .OrderByDescending(post => post.CreatedAtUtc)
                .ThenByDescending(post => post.Id)
                .Take(MaximumPageSize * 2)
                .ToListAsync(cancellationToken);
            if (candidates.Count == 0)
            {
                break;
            }

            foreach (var candidate in candidates)
            {
                scanCursor = new HashtagCursor(candidate.CreatedAtUtc, candidate.Id);
                if (await CanViewPostAsync(candidate, viewer, cancellationToken))
                {
                    visible.Add(candidate);
                    if (visible.Count > limit)
                    {
                        break;
                    }
                }
            }

            if (visible.Count > limit || candidates.Count < MaximumPageSize * 2)
            {
                break;
            }
        }

        var items = visible.Take(limit).ToList();
        var nextCursor = visible.Count > limit
            ? EncodeCursor(new HashtagCursor(items[^1].CreatedAtUtc, items[^1].Id), viewerUserId, $"hashtag:{tag}")
            : null;
        return ApplicationResult<AccessiblePostsPage>.Success(new(items, nextCursor));
    }

    public async Task<IReadOnlyList<HashtagSearchResult>> SearchHashtagsAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var normalized = query.Trim().TrimStart('#').ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return [];
        }

        return await dbContext.Hashtags.AsNoTracking()
            .Where(hashtag => EF.Functions.ILike(hashtag.NormalizedName, normalized + "%"))
            .OrderBy(hashtag => hashtag.NormalizedName)
            .Take(limit)
            .Select(hashtag => new HashtagSearchResult(hashtag.NormalizedName, hashtag.DisplayName))
            .ToListAsync(cancellationToken);
    }

    public async Task SynchronizePostMetadataAsync(
        Guid postId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var post = await dbContext.Posts.SingleOrDefaultAsync(
            item => item.Id == postId && item.DeletedAtUtc == null,
            cancellationToken);
        if (post is null)
        {
            return;
        }

        await SynchronizeMentionsAsync(
            MentionSourceType.POST, post.Id, post.Content, actorUserId, post, cancellationToken);
        await SynchronizeHashtagsAsync(post, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SynchronizeCommentMentionsAsync(
        Guid commentId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var comment = await dbContext.Comments.SingleOrDefaultAsync(
            item => item.Id == commentId && item.DeletedAtUtc == null,
            cancellationToken);
        if (comment is null)
        {
            return;
        }

        var post = await dbContext.Posts.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == comment.PostId && item.DeletedAtUtc == null,
            cancellationToken);
        if (post is null)
        {
            return;
        }

        await SynchronizeMentionsAsync(
            MentionSourceType.COMMENT, comment.Id, comment.Content, actorUserId, post, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SynchronizeMentionsAsync(
        MentionSourceType sourceType,
        Guid sourceId,
        string content,
        Guid actorUserId,
        Post accessiblePost,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.ContentMentions
            .Where(mention => mention.SourceType == sourceType && mention.SourceId == sourceId)
            .ToListAsync(cancellationToken);
        var previouslyMentioned = existing.Select(mention => mention.MentionedUserId).ToHashSet();
        dbContext.ContentMentions.RemoveRange(existing);

        var parsed = ParseMentions(content);
        if (parsed.Count == 0)
        {
            return;
        }

        var requestedUsernames = parsed.Select(mention => mention.Username.ToLowerInvariant()).Distinct().ToArray();
        var profiles = await dbContext.UserProfiles.AsNoTracking()
            .Where(profile => requestedUsernames.Contains(profile.Username.ToLower()))
            .Select(profile => new MentionedProfile(profile.UserId, profile.Username))
            .ToListAsync(cancellationToken);
        var profileByUsername = profiles.ToDictionary(profile => profile.Username, StringComparer.OrdinalIgnoreCase);
        var viewerByTarget = new Dictionary<Guid, PostViewerContext>();
        var canAccessByTarget = new Dictionary<Guid, bool>();
        var resolved = new List<ResolvedMention>();
        foreach (var mention in parsed)
        {
            if (!profileByUsername.TryGetValue(mention.Username, out var profile))
            {
                continue;
            }

            if (!viewerByTarget.TryGetValue(profile.UserId, out var targetViewer))
            {
                targetViewer = await CreateViewerContextAsync(profile.UserId, cancellationToken);
                viewerByTarget[profile.UserId] = targetViewer;
                canAccessByTarget[profile.UserId] = await CanViewPostAsync(
                    accessiblePost, targetViewer, cancellationToken);
            }

            if (!canAccessByTarget[profile.UserId] || targetViewer.BlockedUserIds.Contains(actorUserId))
            {
                continue;
            }

            resolved.Add(new(profile.UserId, mention.StartIndex, mention.Length));
        }

        foreach (var mention in resolved)
        {
            dbContext.ContentMentions.Add(new ContentMention(
                sourceType, sourceId, mention.UserId, mention.StartIndex, mention.Length));
        }

        var notificationType = sourceType == MentionSourceType.POST
            ? NotificationType.POST_MENTION
            : NotificationType.COMMENT_MENTION;
        var entityType = sourceType == MentionSourceType.POST
            ? NotificationEntityType.POST
            : NotificationEntityType.COMMENT;
        var notifications = new List<Notification>();
        foreach (var recipientUserId in resolved.Select(mention => mention.UserId).Distinct()
                     .Where(userId => !previouslyMentioned.Contains(userId)))
        {
            var notification = await notificationService.QueueAsync(
                recipientUserId, actorUserId, notificationType, entityType, sourceId, cancellationToken);
            if (notification is not null)
            {
                notifications.Add(notification);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        foreach (var notification in notifications)
        {
            await notificationService.PublishAsync(notification, cancellationToken);
        }
    }

    private async Task SynchronizeHashtagsAsync(Post post, CancellationToken cancellationToken)
    {
        var existing = await dbContext.PostHashtags.Where(item => item.PostId == post.Id).ToListAsync(cancellationToken);
        dbContext.PostHashtags.RemoveRange(existing);

        var parsed = ParseHashtags(post.Content);
        if (parsed.Count == 0)
        {
            return;
        }

        var names = parsed.Select(tag => tag.NormalizedName).Distinct().ToArray();
        var hashtags = await dbContext.Hashtags
            .Where(hashtag => names.Contains(hashtag.NormalizedName))
            .ToDictionaryAsync(hashtag => hashtag.NormalizedName, cancellationToken);
        foreach (var parsedTag in parsed)
        {
            if (!hashtags.TryGetValue(parsedTag.NormalizedName, out var hashtag))
            {
                hashtag = new Hashtag(
                    Guid.NewGuid(), parsedTag.NormalizedName, parsedTag.DisplayName, timeProvider.GetUtcNow());
                dbContext.Hashtags.Add(hashtag);
                hashtags[parsedTag.NormalizedName] = hashtag;
            }
        }

        foreach (var hashtagId in hashtags.Values.Select(hashtag => hashtag.Id).Distinct())
        {
            dbContext.PostHashtags.Add(new PostHashtag(post.Id, hashtagId));
        }
    }

    private async Task<bool> CanViewPostAsync(
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

        return PostVisibility.CanDirectlyAccess(post, viewer);
    }

    private async Task<PostViewerContext> CreateViewerContextAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var relationships = await friendsService.GetAccessSnapshotAsync(userId, cancellationToken);
        return new PostViewerContext(userId, relationships.FriendUserIds, relationships.BlockedUserIds);
    }

    private async Task<PostViewerContext?> CreateOptionalViewerContextAsync(
        Guid? userId,
        CancellationToken cancellationToken) =>
        userId is null ? null : await CreateViewerContextAsync(userId.Value, cancellationToken);

    private async Task<DateTimeOffset> SavedAtUtcForAsync(
        Guid userId,
        Guid postId,
        CancellationToken cancellationToken) =>
        await dbContext.PostSaves.AsNoTracking()
            .Where(save => save.UserId == userId && save.PostId == postId)
            .Select(save => save.SavedAtUtc)
            .SingleAsync(cancellationToken);

    private TCursor? DecodeCursor<TCursor>(string? value, Guid? viewerUserId, string purpose)
        where TCursor : class
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            if (value.Length is < 1 or > 4096)
            {
                throw new FormatException();
            }

            var cursor = JsonSerializer.Deserialize<TCursor>(CreateCursorProtector(viewerUserId, purpose).Unprotect(value));
            return cursor ?? throw new FormatException();
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or ArgumentException)
        {
            throw new FormatException("The cursor is invalid.", exception);
        }
    }

    private string EncodeCursor<TCursor>(TCursor cursor, Guid? viewerUserId, string purpose) =>
        CreateCursorProtector(viewerUserId, purpose).Protect(JsonSerializer.Serialize(cursor));

    private IDataProtector CreateCursorProtector(Guid? viewerUserId, string purpose) =>
        protectionProvider.CreateProtector(
            "Fookbase.SocialInteractions", CursorVersion.ToString(),
            viewerUserId?.ToString("N") ?? "anonymous", purpose);

    private static List<ParsedMention> ParseMentions(string content) =>
        MentionPattern.Matches(content)
            .Take(MaximumMentionsPerContent)
            .Select(match => new ParsedMention(match.Groups["username"].Value, match.Index, match.Length))
            .ToList();

    private static List<ParsedHashtag> ParseHashtags(string content) =>
        HashtagPattern.Matches(content)
            .Select(match => match.Groups["tag"].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaximumHashtagsPerPost)
            .Select(tag => new ParsedHashtag(tag.ToLowerInvariant(), tag))
            .ToList();

    private static bool TryNormalizeHashtag(string value, out string normalizedName)
    {
        normalizedName = value.Trim().TrimStart('#').ToLowerInvariant();
        return normalizedName.Length is > 0 and <= 50 &&
            normalizedName.All(character => char.IsLetterOrDigit(character) || character == '_');
    }

    private static ApplicationResult PostNotFound() =>
        ApplicationResult.Failure(new ApplicationError(
            "post_not_found", "The post was not found.", ApplicationErrorType.NOT_FOUND));

    private static ApplicationResult<T> PostNotFound<T>() =>
        ApplicationResult<T>.Failure(new ApplicationError(
            "post_not_found", "The post was not found.", ApplicationErrorType.NOT_FOUND));

    private static ApplicationResult<T> Validation<T>(string code, string message) =>
        ApplicationResult<T>.Failure(new ApplicationError(code, message, ApplicationErrorType.VALIDATION));

    private sealed record SavedCursor(DateTimeOffset SavedAtUtc, Guid PostId);
    private sealed record HashtagCursor(DateTimeOffset CreatedAtUtc, Guid PostId);
    private sealed record ParsedMention(string Username, int StartIndex, int Length);
    private sealed record ResolvedMention(Guid UserId, int StartIndex, int Length);
    private sealed record MentionedProfile(Guid UserId, string Username);
    private sealed record ParsedHashtag(string NormalizedName, string DisplayName);
}

public sealed record AccessiblePostsPage(IReadOnlyList<Post> Posts, string? NextCursor);

public sealed record CreatedPostShare(PostShare Share, Post OriginalPost);

public sealed record HashtagSearchResult(string NormalizedName, string DisplayName);
