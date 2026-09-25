using System.Text;
using System.Text.Json;
using Fookbase.Api.Modules.Friends.DTOs.Responses;
using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Messages.DTOs.Responses;
using Fookbase.Api.Modules.Messages.Services;
using Fookbase.Api.Modules.Notifications.Entities;
using Fookbase.Api.Modules.Notifications.Services;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Stories.Config;
using Fookbase.Api.Modules.Stories.DTOs.Responses;
using Fookbase.Api.Modules.Stories.Entities;
using Fookbase.Api.Persistence;
using Fookbase.Api.Shared.ErrorHandling;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Stories.Services;

public sealed class StoriesService(
    FookbaseDbContext dbContext,
    FriendsService friendsService,
    MediaService mediaService,
    MessagesService messagesService,
    NotificationService notificationService,
    StoriesOptions options,
    TimeProvider timeProvider)
{
    public const int DefaultPageSize = 30;
    public const int MaximumPageSize = 100;

    public async Task<ApplicationResult<StoryResponse>> CreateAsync(
        Guid actorUserId,
        Guid mediaId,
        string? caption,
        string privacy,
        CancellationToken cancellationToken = default)
    {
        if (caption?.Trim().Length > Story.MaximumCaptionLength)
        {
            return Validation<StoryResponse>("invalid_story_caption",
                $"Story caption cannot exceed {Story.MaximumCaptionLength} characters.");
        }

        if (!TryParsePrivacy(privacy, out var parsedPrivacy))
        {
            return Validation<StoryResponse>("invalid_story_privacy",
                "Story privacy must be one of: public, friends, onlyMe.");
        }

        var mediaValidation = await mediaService.ValidateStoryMediaAsync(
            actorUserId, mediaId, cancellationToken);
        if (!mediaValidation.Succeeded)
        {
            return ApplicationResult<StoryResponse>.Failure(mediaValidation.Error!);
        }

        var now = timeProvider.GetUtcNow();
        var story = Story.Create(
            Guid.NewGuid(), actorUserId, mediaId, caption, parsedPrivacy, now,
            now.AddHours(options.LifetimeHours));
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            dbContext.Stories.Add(story);
            dbContext.StoryMediaReferences.Add(StoryMediaReference.Create(story.Id, mediaId, now));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ApplicationResult<StoryResponse>.Success(
                (await LoadResponsesAsync([story], actorUserId, cancellationToken))[0]);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<ApplicationResult<StoryTrayResponse>> GetActiveTrayAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var access = await CreateAccessContextAsync(viewerUserId, cancellationToken);
        var authorIds = access.FriendUserIds.Append(viewerUserId)
            .Except(access.BlockedUserIds)
            .Append(viewerUserId)
            .Distinct()
            .ToArray();
        var friendUserIds = access.FriendUserIds;
        var blockedUserIds = access.BlockedUserIds;
        var stories = await dbContext.Stories.AsNoTracking()
            .Where(story =>
                story.DeletedAtUtc == null &&
                story.ExpiresAtUtc > now &&
                authorIds.Contains(story.AuthorUserId) &&
                (story.AuthorUserId == viewerUserId ||
                 (!blockedUserIds.Contains(story.AuthorUserId) &&
                  friendUserIds.Contains(story.AuthorUserId) &&
                  (story.Privacy == PostPrivacy.Public || story.Privacy == PostPrivacy.Friends))))
            .OrderBy(story => story.CreatedAtUtc)
            .ThenBy(story => story.Id)
            .ToListAsync(cancellationToken);

        var responses = await LoadResponsesAsync(stories, viewerUserId, cancellationToken);
        var groups = responses
            .GroupBy(story => story.Author.UserId)
            .Select(group => new StoryTrayAuthorResponse(
                group.First().Author,
                group.Any(story => !story.IsViewed && !story.CanManage),
                group.OrderBy(story => story.CreatedAtUtc).ThenBy(story => story.Id).ToArray()))
            .OrderByDescending(group => group.HasUnseenStories)
            .ThenByDescending(group => group.Author.UserId == viewerUserId)
            .ThenByDescending(group => group.Stories.Max(story => story.CreatedAtUtc))
            .ToArray();
        return ApplicationResult<StoryTrayResponse>.Success(new StoryTrayResponse(groups));
    }

    public async Task<ApplicationResult<StoryResponse>> GetAsync(
        Guid viewerUserId,
        Guid storyId,
        CancellationToken cancellationToken = default)
    {
        var story = await dbContext.Stories.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == storyId,
            cancellationToken);
        if (story is null || !await CanAccessAsync(story, viewerUserId, false, cancellationToken))
        {
            return NotFound<StoryResponse>();
        }

        return ApplicationResult<StoryResponse>.Success(
            (await LoadResponsesAsync([story], viewerUserId, cancellationToken))[0]);
    }

    public async Task<ApplicationResult> RecordViewAsync(
        Guid viewerUserId,
        Guid storyId,
        CancellationToken cancellationToken = default)
    {
        var story = await dbContext.Stories.SingleOrDefaultAsync(item => item.Id == storyId,
            cancellationToken);
        if (story is null || !await CanAccessAsync(story, viewerUserId, false, cancellationToken))
        {
            return NotFound();
        }

        if (story.AuthorUserId == viewerUserId)
        {
            return ApplicationResult.Success();
        }

        var now = timeProvider.GetUtcNow();
        var view = await dbContext.StoryViews.SingleOrDefaultAsync(
            item => item.StoryId == storyId && item.ViewerUserId == viewerUserId,
            cancellationToken);
        if (view is null)
        {
            dbContext.StoryViews.Add(StoryView.Create(storyId, viewerUserId, now));
        }
        else
        {
            view.Refresh(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult<StoryViewersPageResponse>> GetViewersAsync(
        Guid actorUserId,
        Guid storyId,
        string? cursorValue,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidateLimit(limit, out var error))
        {
            return ApplicationResult<StoryViewersPageResponse>.Failure(error!);
        }

        if (!TryDecodeCursor(cursorValue, out ViewerCursor? cursor))
        {
            return Validation<StoryViewersPageResponse>("invalid_story_viewer_cursor",
                "The story viewer cursor is invalid.");
        }

        var story = await dbContext.Stories.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == storyId && item.DeletedAtUtc == null,
            cancellationToken);
        if (story is null)
        {
            return NotFound<StoryViewersPageResponse>();
        }

        if (story.AuthorUserId != actorUserId)
        {
            return Forbidden<StoryViewersPageResponse>("story_viewers_forbidden",
                "Only the story author can see its viewers.");
        }

        var query = dbContext.StoryViews.AsNoTracking()
            .Where(view => view.StoryId == storyId);
        if (cursor is not null)
        {
            query = query.Where(view =>
                view.ViewedAtUtc < cursor.ViewedAtUtc ||
                (view.ViewedAtUtc == cursor.ViewedAtUtc &&
                 view.ViewerUserId.CompareTo(cursor.ViewerUserId) < 0));
        }

        var candidates = await query
            .OrderByDescending(view => view.ViewedAtUtc)
            .ThenByDescending(view => view.ViewerUserId)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        var page = candidates.Take(limit).ToArray();
        var viewerIds = page.Select(view => view.ViewerUserId).ToArray();
        var profiles = await LoadProfilesAsync(viewerIds, cancellationToken);
        var reactions = viewerIds.Length == 0
            ? new Dictionary<Guid, StoryReaction>()
            : await dbContext.StoryReactions.AsNoTracking()
                .Where(reaction => reaction.StoryId == storyId && viewerIds.Contains(reaction.UserId))
                .ToDictionaryAsync(reaction => reaction.UserId, cancellationToken);
        var items = page.Select(view =>
        {
            var profile = profiles.GetValueOrDefault(view.ViewerUserId) ??
                new ProfileRow(view.ViewerUserId, "unknown", "Unknown", null);
            return new StoryViewerResponse(
                profile.UserId,
                profile.Username,
                profile.DisplayName,
                profile.AvatarUrl,
                view.ViewedAtUtc,
                reactions.GetValueOrDefault(view.ViewerUserId)?.Type.ToString().ToLowerInvariant());
        }).ToArray();
        return ApplicationResult<StoryViewersPageResponse>.Success(new StoryViewersPageResponse(
            items,
            candidates.Count > limit && page.Length > 0
                ? EncodeCursor(new ViewerCursor(page[^1].ViewedAtUtc, page[^1].ViewerUserId))
                : null));
    }

    public async Task<ApplicationResult<StoryResponse>> SetReactionAsync(
        Guid actorUserId,
        Guid storyId,
        string reactionType,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseReaction(reactionType, out var parsedReaction))
        {
            return Validation<StoryResponse>("invalid_story_reaction",
                "Story reaction must be one of: like, love, haha, wow, sad, angry.");
        }

        var story = await dbContext.Stories.SingleOrDefaultAsync(item => item.Id == storyId,
            cancellationToken);
        if (story is null || !await CanAccessAsync(story, actorUserId, false, cancellationToken))
        {
            return NotFound<StoryResponse>();
        }

        if (story.AuthorUserId == actorUserId)
        {
            return Forbidden<StoryResponse>("cannot_react_to_own_story",
                "You cannot react to your own story.");
        }

        var now = timeProvider.GetUtcNow();
        var reaction = await dbContext.StoryReactions.SingleOrDefaultAsync(
            item => item.StoryId == storyId && item.UserId == actorUserId,
            cancellationToken);
        if (reaction is null)
        {
            dbContext.StoryReactions.Add(StoryReaction.Create(storyId, actorUserId, parsedReaction, now));
        }
        else
        {
            reaction.Change(parsedReaction, now);
        }

        var notification = await notificationService.QueueAsync(
            story.AuthorUserId,
            actorUserId,
            NotificationType.StoryReaction,
            NotificationEntityType.Story,
            story.Id,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (notification is not null)
        {
            await notificationService.PublishAsync(notification, cancellationToken);
        }

        return ApplicationResult<StoryResponse>.Success(
            (await LoadResponsesAsync([story], actorUserId, cancellationToken))[0]);
    }

    public async Task<ApplicationResult> RemoveReactionAsync(
        Guid actorUserId,
        Guid storyId,
        CancellationToken cancellationToken = default)
    {
        var story = await dbContext.Stories.AsNoTracking().SingleOrDefaultAsync(item => item.Id == storyId,
            cancellationToken);
        if (story is null || !await CanAccessAsync(story, actorUserId, false, cancellationToken))
        {
            return NotFound();
        }

        var reaction = await dbContext.StoryReactions.SingleOrDefaultAsync(
            item => item.StoryId == storyId && item.UserId == actorUserId,
            cancellationToken);
        if (reaction is not null)
        {
            dbContext.StoryReactions.Remove(reaction);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult<MessageResponse>> ReplyAsync(
        Guid actorUserId,
        Guid storyId,
        string? content,
        CancellationToken cancellationToken = default)
    {
        var story = await dbContext.Stories.AsNoTracking().SingleOrDefaultAsync(item => item.Id == storyId,
            cancellationToken);
        if (story is null || !await CanAccessAsync(story, actorUserId, false, cancellationToken))
        {
            return NotFound<MessageResponse>();
        }

        if (story.AuthorUserId == actorUserId)
        {
            return Forbidden<MessageResponse>("cannot_reply_to_own_story",
                "You cannot reply to your own story.");
        }

        return await messagesService.SendStoryReplyAsync(
            actorUserId,
            story.AuthorUserId,
            story.Id,
            content,
            cancellationToken);
    }

    public async Task<ApplicationResult> DeleteAsync(
        Guid actorUserId,
        Guid storyId,
        CancellationToken cancellationToken = default)
    {
        var story = await dbContext.Stories.SingleOrDefaultAsync(item => item.Id == storyId,
            cancellationToken);
        if (story is null || story.DeletedAtUtc is not null)
        {
            return NotFound();
        }

        if (story.AuthorUserId != actorUserId)
        {
            return Forbidden("story_delete_forbidden", "Only the story author can delete it.");
        }

        story.Delete(timeProvider.GetUtcNow());
        await dbContext.StoryMediaReferences
            .Where(reference => reference.StoryId == storyId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult<StoryArchivePageResponse>> GetArchiveAsync(
        Guid actorUserId,
        string? cursorValue,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidateLimit(limit, out var error))
        {
            return ApplicationResult<StoryArchivePageResponse>.Failure(error!);
        }

        if (!TryDecodeCursor(cursorValue, out ArchiveCursor? cursor))
        {
            return Validation<StoryArchivePageResponse>("invalid_story_archive_cursor",
                "The story archive cursor is invalid.");
        }

        var now = timeProvider.GetUtcNow();
        var query = dbContext.Stories.AsNoTracking()
            .Where(story =>
                story.AuthorUserId == actorUserId &&
                story.DeletedAtUtc == null &&
                story.ExpiresAtUtc <= now);
        if (cursor is not null)
        {
            query = query.Where(story =>
                story.CreatedAtUtc < cursor.CreatedAtUtc ||
                (story.CreatedAtUtc == cursor.CreatedAtUtc && story.Id.CompareTo(cursor.Id) < 0));
        }

        var candidates = await query
            .OrderByDescending(story => story.CreatedAtUtc)
            .ThenByDescending(story => story.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        var page = candidates.Take(limit).ToArray();
        return ApplicationResult<StoryArchivePageResponse>.Success(new StoryArchivePageResponse(
            await LoadResponsesAsync(page, actorUserId, cancellationToken),
            candidates.Count > limit && page.Length > 0
                ? EncodeCursor(new ArchiveCursor(page[^1].CreatedAtUtc, page[^1].Id))
                : null));
    }

    public async Task<ApplicationResult<StoryMediaAccessResponse>> GetMediaAccessAsync(
        Guid actorUserId,
        Guid storyId,
        bool poster,
        CancellationToken cancellationToken = default)
    {
        var story = await dbContext.Stories.AsNoTracking().SingleOrDefaultAsync(item => item.Id == storyId,
            cancellationToken);
        if (story is null || !await CanAccessAsync(story, actorUserId, true, cancellationToken))
        {
            return NotFound<StoryMediaAccessResponse>();
        }

        var access = poster
            ? await mediaService.CreatePosterReadUrlAsync(story.MediaId, cancellationToken)
            : await mediaService.CreateReadUrlAsync(story.MediaId, cancellationToken);
        if (!access.Succeeded)
        {
            return NotFound<StoryMediaAccessResponse>();
        }

        return ApplicationResult<StoryMediaAccessResponse>.Success(new StoryMediaAccessResponse(
            access.Value!.MediaId,
            access.Value.Url,
            access.Value.ExpiresAtUtc,
            access.Value.MediaType,
            access.Value.ContentType));
    }

    private async Task<IReadOnlyList<StoryResponse>> LoadResponsesAsync(
        IReadOnlyCollection<Story> stories,
        Guid viewerUserId,
        CancellationToken cancellationToken)
    {
        if (stories.Count == 0)
        {
            return [];
        }

        var storyIds = stories.Select(story => story.Id).ToArray();
        var authorIds = stories.Select(story => story.AuthorUserId).Distinct().ToArray();
        var mediaIds = stories.Select(story => story.MediaId).Distinct().ToArray();
        var media = await dbContext.MediaAssets.AsNoTracking()
            .Where(asset => mediaIds.Contains(asset.Id) && asset.Status == MediaStatus.Ready &&
                            asset.DeletedAtUtc == null)
            .ToDictionaryAsync(asset => asset.Id, cancellationToken);
        var profiles = await LoadProfilesAsync(authorIds, cancellationToken);
        var viewedStoryIds = await dbContext.StoryViews.AsNoTracking()
            .Where(view => view.ViewerUserId == viewerUserId && storyIds.Contains(view.StoryId))
            .Select(view => view.StoryId)
            .ToHashSetAsync(cancellationToken);
        var ownedStoryIds = stories
            .Where(story => story.AuthorUserId == viewerUserId)
            .Select(story => story.Id)
            .ToArray();
        var viewerCounts = ownedStoryIds.Length == 0
            ? new Dictionary<Guid, int>()
            : await dbContext.StoryViews.AsNoTracking()
                .Where(view => ownedStoryIds.Contains(view.StoryId))
                .GroupBy(view => view.StoryId)
                .Select(group => new CountRow(group.Key, group.Count()))
                .ToDictionaryAsync(row => row.Id, row => row.Count, cancellationToken);
        var reactionCounts = await dbContext.StoryReactions.AsNoTracking()
            .Where(reaction => storyIds.Contains(reaction.StoryId))
            .GroupBy(reaction => reaction.StoryId)
            .Select(group => new CountRow(group.Key, group.Count()))
            .ToDictionaryAsync(row => row.Id, row => row.Count, cancellationToken);
        var viewerReactions = await dbContext.StoryReactions.AsNoTracking()
            .Where(reaction => storyIds.Contains(reaction.StoryId) && reaction.UserId == viewerUserId)
            .ToDictionaryAsync(reaction => reaction.StoryId, cancellationToken);

        var responses = new List<StoryResponse>(stories.Count);
        foreach (var story in stories)
        {
            if (!media.TryGetValue(story.MediaId, out var asset))
            {
                continue;
            }

            if (asset.MediaType == MediaType.Video &&
                (string.IsNullOrWhiteSpace(asset.ProcessedObjectKey) ||
                 string.IsNullOrWhiteSpace(asset.PosterObjectKey)))
            {
                continue;
            }

            var profile = profiles.GetValueOrDefault(story.AuthorUserId) ??
                new ProfileRow(story.AuthorUserId, "unknown", "Unknown", null);
            var canManage = story.AuthorUserId == viewerUserId;
            responses.Add(new StoryResponse(
                story.Id,
                new StoryAuthorResponse(profile.UserId, profile.Username, profile.DisplayName, profile.AvatarUrl),
                story.Caption,
                ToPrivacyString(story.Privacy),
                story.CreatedAtUtc,
                story.ExpiresAtUtc,
                new StoryMediaResponse(
                    asset.Id,
                    asset.MediaType.ToString().ToLowerInvariant(),
                    asset.MediaType == MediaType.Video ? "video/mp4" : asset.ContentType,
                    asset.DurationMs,
                    asset.Width,
                    asset.Height,
                    $"/api/stories/{story.Id}/media/access",
                    asset.MediaType == MediaType.Video
                        ? $"/api/stories/{story.Id}/media/poster/access"
                        : null),
                !canManage && viewedStoryIds.Contains(story.Id),
                canManage,
                canManage ? viewerCounts.GetValueOrDefault(story.Id) : null,
                reactionCounts.GetValueOrDefault(story.Id),
                viewerReactions.GetValueOrDefault(story.Id)?.Type.ToString().ToLowerInvariant()));
        }

        return responses;
    }

    private async Task<Dictionary<Guid, ProfileRow>> LoadProfilesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        return await dbContext.UserProfiles.AsNoTracking()
            .Where(profile => userIds.Contains(profile.UserId))
            .Select(profile => new ProfileRow(
                profile.UserId,
                profile.Username,
                profile.DisplayName,
                profile.AvatarMediaId == null ? profile.AvatarUrl : $"/api/users/{profile.UserId}/avatar"))
            .ToDictionaryAsync(profile => profile.UserId, cancellationToken);
    }

    private async Task<StoryAccessContext> CreateAccessContextAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken)
    {
        var snapshot = await friendsService.GetAccessSnapshotAsync(viewerUserId, cancellationToken);
        return new StoryAccessContext(viewerUserId, snapshot.FriendUserIds, snapshot.BlockedUserIds);
    }

    private async Task<bool> CanAccessAsync(
        Story story,
        Guid viewerUserId,
        bool allowExpiredForAuthor,
        CancellationToken cancellationToken)
    {
        if (story.DeletedAtUtc is not null)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        if (story.AuthorUserId == viewerUserId)
        {
            return allowExpiredForAuthor || story.ExpiresAtUtc > now;
        }

        if (story.ExpiresAtUtc <= now)
        {
            return false;
        }

        var access = await CreateAccessContextAsync(viewerUserId, cancellationToken);
        if (access.BlockedUserIds.Contains(story.AuthorUserId))
        {
            return false;
        }

        return story.Privacy == PostPrivacy.Public ||
               (story.Privacy == PostPrivacy.Friends &&
                access.FriendUserIds.Contains(story.AuthorUserId));
    }

    private static bool TryParsePrivacy(string? value, out PostPrivacy privacy)
    {
        privacy = value?.Trim().ToLowerInvariant() switch
        {
            "public" => PostPrivacy.Public,
            "friends" => PostPrivacy.Friends,
            "onlyme" => PostPrivacy.OnlyMe,
            _ => (PostPrivacy)(-1)
        };
        return Enum.IsDefined(privacy);
    }

    private static bool TryParseReaction(string? value, out StoryReactionType reaction)
    {
        reaction = value?.Trim().ToLowerInvariant() switch
        {
            "like" => StoryReactionType.Like,
            "love" => StoryReactionType.Love,
            "haha" => StoryReactionType.Haha,
            "wow" => StoryReactionType.Wow,
            "sad" => StoryReactionType.Sad,
            "angry" => StoryReactionType.Angry,
            _ => (StoryReactionType)(-1)
        };
        return Enum.IsDefined(reaction);
    }

    private static string ToPrivacyString(PostPrivacy privacy) => privacy switch
    {
        PostPrivacy.Public => "public",
        PostPrivacy.Friends => "friends",
        _ => "onlyMe"
    };

    private static bool TryValidateLimit(int limit, out ApplicationError? error)
    {
        error = limit is < 1 or > MaximumPageSize
            ? new ApplicationError(ErrorCode.InvalidPagination,
                $"Limit must be between 1 and {MaximumPageSize}.", ApplicationErrorType.Validation)
            : null;
        return error is null;
    }

    private static bool TryDecodeCursor<T>(string? value, out T? cursor)
    {
        cursor = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        try
        {
            var base64 = value.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            cursor = JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(Convert.FromBase64String(base64)));
            return cursor is not null;
        }
        catch (ArgumentException) { return false; }
        catch (FormatException) { return false; }
        catch (JsonException) { return false; }
    }

    private static string EncodeCursor<T>(T cursor) => Convert
        .ToBase64String(JsonSerializer.SerializeToUtf8Bytes(cursor))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    private static ApplicationResult NotFound() => ApplicationResult.Failure(new ApplicationError(
        "story_not_found", "The story was not found.", ApplicationErrorType.NotFound));

    private static ApplicationResult<T> NotFound<T>() => ApplicationResult<T>.Failure(new ApplicationError(
        "story_not_found", "The story was not found.", ApplicationErrorType.NotFound));

    private static ApplicationResult Validation(string code, string message) => ApplicationResult.Failure(
        new ApplicationError(code, message, ApplicationErrorType.Validation));

    private static ApplicationResult<T> Validation<T>(string code, string message) => ApplicationResult<T>.Failure(
        new ApplicationError(code, message, ApplicationErrorType.Validation));

    private static ApplicationResult Forbidden(string code, string message) => ApplicationResult.Failure(
        new ApplicationError(code, message, ApplicationErrorType.Forbidden));

    private static ApplicationResult<T> Forbidden<T>(string code, string message) => ApplicationResult<T>.Failure(
        new ApplicationError(code, message, ApplicationErrorType.Forbidden));

    private sealed record StoryAccessContext(
        Guid ViewerUserId,
        IReadOnlySet<Guid> FriendUserIds,
        IReadOnlySet<Guid> BlockedUserIds);

    private sealed record ProfileRow(Guid UserId, string Username, string DisplayName, string? AvatarUrl);
    private sealed record CountRow(Guid Id, int Count);
    private sealed record ViewerCursor(DateTimeOffset ViewedAtUtc, Guid ViewerUserId);
    private sealed record ArchiveCursor(DateTimeOffset CreatedAtUtc, Guid Id);
}
