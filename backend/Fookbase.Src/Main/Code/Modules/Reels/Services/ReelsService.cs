using System.Globalization;
using System.Text;
using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Posts.Common;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Modules.Reels.DTOs.Responses;
using Fookbase.Api.Modules.Reels.Entities;
using Fookbase.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Reels.Services;

public sealed class ReelsService(
    FookbaseDbContext dbContext,
    FriendsService friendsService,
    MediaService mediaService,
    SocialInteractionsService socialInteractionsService,
    TimeProvider timeProvider)
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 50;

    public async Task<ApplicationResult<ReelResponse>> CreateAsync(
        Guid actorUserId,
        string? caption,
        string privacy,
        Guid videoMediaId,
        CancellationToken cancellationToken = default)
    {
        var normalizedCaption = caption?.Trim() ?? string.Empty;
        if (normalizedCaption.Length > Post.MaximumContentLength)
        {
            return Validation<ReelResponse>("invalid_reel_caption",
                $"Reel caption cannot exceed {Post.MaximumContentLength} characters.");
        }

        if (!TryParsePrivacy(privacy, out var parsedPrivacy))
        {
            return Validation<ReelResponse>("invalid_post_privacy",
                "Post privacy must be one of: public, friends, onlyMe.");
        }

        var mediaValidation = await mediaService.ValidateReelVideoAsync(
            actorUserId, videoMediaId, cancellationToken);
        if (!mediaValidation.Succeeded)
        {
            return ApplicationResult<ReelResponse>.Failure(ToPostError(mediaValidation.Error!));
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var post = Post.CreateReel(
                Guid.NewGuid(), actorUserId, normalizedCaption, parsedPrivacy, timeProvider.GetUtcNow());
            dbContext.Posts.Add(post);
            dbContext.PostMedia.Add(PostMedia.Create(post.Id, videoMediaId, 0));
            await dbContext.SaveChangesAsync(cancellationToken);
            await socialInteractionsService.SynchronizePostMetadataAsync(post.Id, actorUserId, cancellationToken);

            var references = await mediaService.SynchronizePostReferencesAsync(
                actorUserId, post.Id, [videoMediaId], cancellationToken);
            if (!references.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ApplicationResult<ReelResponse>.Failure(ToPostError(references.Error!));
            }

            await transaction.CommitAsync(cancellationToken);
            return ApplicationResult<ReelResponse>.Success(
                (await LoadResponsesAsync([post], actorUserId, cancellationToken))[0]);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<ApplicationResult<ReelResponse>> GetAsync(
        Guid? viewerUserId,
        Guid reelId,
        CancellationToken cancellationToken = default)
    {
        var viewer = await CreateViewerContextAsync(viewerUserId, cancellationToken);
        var reel = await ReelMediaQuery.ApplyReadyMedia(dbContext.Posts.AsNoTracking(), dbContext)
            .SingleOrDefaultAsync(
            post => post.Id == reelId && post.PostType == PostType.Reel &&
                    post.ContainerType == PostContainerType.Profile && post.DeletedAtUtc == null,
            cancellationToken);
        if (reel is null || !PostVisibility.CanDirectlyAccess(reel, viewer))
        {
            return NotFound<ReelResponse>();
        }

        return ApplicationResult<ReelResponse>.Success(
            (await LoadResponsesAsync([reel], viewerUserId, cancellationToken))[0]);
    }

    public async Task<ApplicationResult<ReelPageResponse>> GetFeedAsync(
        Guid viewerUserId,
        string? cursorValue,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit < 1 || limit > MaximumPageSize)
        {
            return Validation<ReelPageResponse>("invalid_pagination",
                $"Limit must be between 1 and {MaximumPageSize}.");
        }

        ReelCursor? cursor;
        try
        {
            cursor = string.IsNullOrWhiteSpace(cursorValue) ? null : DecodeCursor(cursorValue);
        }
        catch (FormatException)
        {
            return Validation<ReelPageResponse>("invalid_reel_cursor", "The reel cursor is invalid.");
        }

        var viewer = await CreateRequiredViewerContextAsync(viewerUserId, cancellationToken);
        var query = ReelMediaQuery.ApplyReadyMedia(
            PostVisibility.ApplyDirectAccess(dbContext.Posts.AsNoTracking(), viewer), dbContext)
            .Where(post => post.PostType == PostType.Reel);
        if (cursor is not null)
        {
            query = query.Where(post =>
                post.CreatedAtUtc < cursor.CreatedAtUtc ||
                (post.CreatedAtUtc == cursor.CreatedAtUtc && post.Id.CompareTo(cursor.Id) < 0));
        }

        var candidates = await query
            .OrderByDescending(post => post.CreatedAtUtc)
            .ThenByDescending(post => post.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        var reels = candidates.Take(limit).ToList();
        return ApplicationResult<ReelPageResponse>.Success(new ReelPageResponse(
            await LoadResponsesAsync(reels, viewerUserId, cancellationToken),
            candidates.Count > limit ? EncodeCursor(reels[^1]) : null));
    }

    public async Task<ApplicationResult<ReelMediaAccessResponse>> GetMediaAccessAsync(
        Guid viewerUserId,
        Guid reelId,
        bool poster,
        CancellationToken cancellationToken = default)
    {
        var viewer = await CreateRequiredViewerContextAsync(viewerUserId, cancellationToken);
        var reel = await dbContext.Posts.AsNoTracking().SingleOrDefaultAsync(
            post => post.Id == reelId && post.PostType == PostType.Reel &&
                    post.ContainerType == PostContainerType.Profile && post.DeletedAtUtc == null,
            cancellationToken);
        if (reel is null || !PostVisibility.CanDirectlyAccess(reel, viewer))
        {
            return NotFound<ReelMediaAccessResponse>();
        }

        var videoMediaId = await GetReadyVideoMediaIdAsync(reelId, cancellationToken);
        if (videoMediaId is null)
        {
            return NotFound<ReelMediaAccessResponse>();
        }

        var access = poster
            ? await mediaService.CreatePosterReadUrlAsync(videoMediaId.Value, cancellationToken)
            : await mediaService.CreateReadUrlAsync(videoMediaId.Value, cancellationToken);
        if (!access.Succeeded)
        {
            return NotFound<ReelMediaAccessResponse>();
        }

        return ApplicationResult<ReelMediaAccessResponse>.Success(new ReelMediaAccessResponse(
            access.Value!.MediaId,
            access.Value.Url,
            access.Value.ExpiresAtUtc,
            access.Value.MediaType,
            access.Value.ContentType));
    }

    public async Task<ApplicationResult> RecordViewAsync(
        Guid viewerUserId,
        Guid reelId,
        int watchDurationMs,
        bool completed,
        bool replayed,
        CancellationToken cancellationToken = default)
    {
        if (watchDurationMs <= 0)
        {
            return Validation("invalid_watch_duration", "Watch duration must be positive.");
        }

        var viewer = await CreateRequiredViewerContextAsync(viewerUserId, cancellationToken);
        var reel = await dbContext.Posts.AsNoTracking().SingleOrDefaultAsync(
            post => post.Id == reelId && post.PostType == PostType.Reel &&
                    post.ContainerType == PostContainerType.Profile && post.DeletedAtUtc == null,
            cancellationToken);
        if (reel is null || !PostVisibility.CanDirectlyAccess(reel, viewer))
        {
            return NotFound();
        }

        var video = await GetReadyVideoAsync(reelId, cancellationToken);
        if (video is null)
        {
            return NotFound();
        }

        if (watchDurationMs > video.DurationMs)
        {
            return Validation("invalid_watch_duration",
                "Watch duration cannot exceed the reel duration.");
        }

        dbContext.ReelViews.Add(ReelView.Create(
            reelId,
            viewerUserId,
            watchDurationMs,
            completed,
            replayed,
            timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult.Success();
    }

    private async Task<IReadOnlyList<ReelResponse>> LoadResponsesAsync(
        IReadOnlyList<Post> reels,
        Guid? viewerUserId,
        CancellationToken cancellationToken)
    {
        if (reels.Count == 0)
        {
            return [];
        }

        var reelIds = reels.Select(reel => reel.Id).ToArray();
        var authorIds = reels.Select(reel => reel.AuthorUserId).Distinct().ToArray();
        var mediaRows = await (
            from postMedia in dbContext.PostMedia.AsNoTracking()
            join asset in ReelMediaQuery.ReadyVideos(dbContext.MediaAssets.AsNoTracking())
                on postMedia.MediaId equals asset.Id
            where reelIds.Contains(postMedia.PostId)
            select new ReelMediaRow(
                postMedia.PostId,
                asset.Id,
                asset.DurationMs ?? 0,
                asset.Width ?? 0,
                asset.Height ?? 0))
            .ToDictionaryAsync(row => row.PostId, cancellationToken);
        var profiles = await dbContext.UserProfiles.AsNoTracking()
            .Where(profile => authorIds.Contains(profile.UserId))
            .Select(profile => new AuthorProfile(
                profile.UserId,
                profile.Username,
                profile.DisplayName,
                profile.AvatarUrl,
                profile.AvatarMediaId))
            .ToDictionaryAsync(profile => profile.UserId, cancellationToken);
        var users = await dbContext.Users.AsNoTracking()
            .Where(user => authorIds.Contains(user.Id))
            .Select(user => new AuthorUser(user.Id, user.UserName))
            .ToDictionaryAsync(user => user.UserId, cancellationToken);
        var commentCounts = await dbContext.Comments.AsNoTracking()
            .Where(comment => reelIds.Contains(comment.PostId) && comment.DeletedAtUtc == null)
            .GroupBy(comment => comment.PostId)
            .Select(group => new CountRow(group.Key, group.Count()))
            .ToDictionaryAsync(row => row.Id, row => row.Count, cancellationToken);
        var reactions = await dbContext.PostReactions.AsNoTracking()
            .Where(reaction => reelIds.Contains(reaction.PostId))
            .GroupBy(reaction => new { reaction.PostId, reaction.Type })
            .Select(group => new ReactionRow(group.Key.PostId, group.Key.Type, group.Count()))
            .ToListAsync(cancellationToken);
        var viewerReactions = viewerUserId is null
            ? []
            : await dbContext.PostReactions.AsNoTracking()
                .Where(reaction => reelIds.Contains(reaction.PostId) && reaction.UserId == viewerUserId.Value)
                .ToDictionaryAsync(
                    reaction => reaction.PostId,
                    reaction => reaction.Type.ToString().ToLowerInvariant(),
                    cancellationToken);
        var viewCounts = await dbContext.ReelViews.AsNoTracking()
            .Where(view => reelIds.Contains(view.ReelPostId))
            .GroupBy(view => view.ReelPostId)
            .Select(group => new ViewCountRow(group.Key, group.LongCount(), group.LongCount(view => view.Completed)))
            .ToDictionaryAsync(row => row.ReelPostId, cancellationToken);

        return reels.Select(reel =>
        {
            var media = mediaRows[reel.Id];
            var profile = profiles.GetValueOrDefault(reel.AuthorUserId);
            var user = users.GetValueOrDefault(reel.AuthorUserId);
            var username = profile?.Username ?? user?.Username ?? reel.AuthorUserId.ToString("N");
            var displayName = profile?.DisplayName ?? username;
            var reactionCounts = reactions
                .Where(reaction => reaction.PostId == reel.Id)
                .ToDictionary(
                    reaction => reaction.Type.ToString().ToLowerInvariant(),
                    reaction => reaction.Count);
            var views = viewCounts.GetValueOrDefault(reel.Id);
            return new ReelResponse(
                reel.Id,
                new ReelAuthorResponse(
                    reel.AuthorUserId,
                    username,
                    displayName,
                    profile?.AvatarMediaId is not null
                        ? "/api/users/" + reel.AuthorUserId + "/avatar"
                        : profile?.AvatarUrl),
                reel.Content,
                PrivacyName(reel.Privacy),
                reel.CreatedAtUtc,
                reel.UpdatedAtUtc,
                new ReelVideoResponse(
                    media.MediaId,
                    media.DurationMs,
                    media.Width,
                    media.Height,
                    "video/mp4",
                    "/api/reels/" + reel.Id + "/video/access",
                    "/api/reels/" + reel.Id + "/poster/access"),
                commentCounts.GetValueOrDefault(reel.Id),
                reactionCounts.Values.Sum(),
                reactionCounts,
                viewerReactions.GetValueOrDefault(reel.Id),
                views?.ViewCount ?? 0,
                views?.CompletionCount ?? 0);
        }).ToList();
    }

    private async Task<Guid?> GetReadyVideoMediaIdAsync(Guid reelId, CancellationToken cancellationToken) =>
        await dbContext.PostMedia.AsNoTracking()
            .Where(postMedia => postMedia.PostId == reelId)
            .Join(
                ReelMediaQuery.ReadyVideos(dbContext.MediaAssets.AsNoTracking()),
                postMedia => postMedia.MediaId,
                asset => asset.Id,
                (postMedia, _) => (Guid?)postMedia.MediaId)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<MediaAsset?> GetReadyVideoAsync(Guid reelId, CancellationToken cancellationToken) =>
        await dbContext.PostMedia.AsNoTracking()
            .Where(postMedia => postMedia.PostId == reelId)
            .Join(
                ReelMediaQuery.ReadyVideos(dbContext.MediaAssets.AsNoTracking()),
                postMedia => postMedia.MediaId,
                asset => asset.Id,
                (_, asset) => asset)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<PostViewerContext?> CreateViewerContextAsync(
        Guid? viewerUserId,
        CancellationToken cancellationToken) =>
        viewerUserId is null ? null : await CreateRequiredViewerContextAsync(viewerUserId.Value, cancellationToken);

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

    private static bool TryParsePrivacy(string privacy, out PostPrivacy parsedPrivacy) =>
        Enum.TryParse(privacy, true, out parsedPrivacy) && Enum.IsDefined(parsedPrivacy);

    private static string PrivacyName(PostPrivacy privacy) => privacy switch
    {
        PostPrivacy.Public => "public",
        PostPrivacy.Friends => "friends",
        PostPrivacy.OnlyMe => "onlyMe",
        _ => throw new ArgumentOutOfRangeException(nameof(privacy), privacy, null)
    };

    private static string EncodeCursor(Post reel)
    {
        var payload = reel.CreatedAtUtc.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture) +
            ":" + reel.Id.ToString("N");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static ReelCursor DecodeCursor(string value)
    {
        try
        {
            var encoded = value.Replace('-', '+').Replace('_', '/');
            encoded = encoded.PadRight(encoded.Length + (4 - encoded.Length % 4) % 4, '=');
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(encoded)).Split(':', 2);
            if (parts.Length != 2 ||
                !long.TryParse(parts[0], CultureInfo.InvariantCulture, out var ticks) ||
                !Guid.TryParseExact(parts[1], "N", out var id))
            {
                throw new FormatException("The reel cursor is invalid.");
            }

            return new ReelCursor(new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc)), id);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException("The reel cursor is invalid.", exception);
        }
    }

    private static ApplicationResult<T> Validation<T>(string code, string message) =>
        ApplicationResult<T>.Failure(new ApplicationError(code, message, ApplicationErrorType.Validation));

    private static ApplicationResult Validation(string code, string message) =>
        ApplicationResult.Failure(new ApplicationError(code, message, ApplicationErrorType.Validation));

    private static ApplicationResult<T> NotFound<T>() =>
        ApplicationResult<T>.Failure(new ApplicationError(
            "reel_not_found", "The reel was not found.", ApplicationErrorType.NotFound));

    private static ApplicationResult NotFound() =>
        ApplicationResult.Failure(new ApplicationError(
            "reel_not_found", "The reel was not found.", ApplicationErrorType.NotFound));

    private static ApplicationError ToPostError(Fookbase.Api.Modules.Media.Common.ApplicationError error) =>
        new(error.Code, error.Message, (ApplicationErrorType)(int)error.Type);

    private sealed record ReelCursor(DateTimeOffset CreatedAtUtc, Guid Id);
    private sealed record ReelMediaRow(Guid PostId, Guid MediaId, long DurationMs, int Width, int Height);
    private sealed record AuthorProfile(Guid UserId, string Username, string DisplayName, string? AvatarUrl, Guid? AvatarMediaId);
    private sealed record AuthorUser(Guid UserId, string? Username);
    private sealed record CountRow(Guid Id, int Count);
    private sealed record ReactionRow(Guid PostId, ReactionType Type, int Count);
    private sealed record ViewCountRow(Guid ReelPostId, long ViewCount, long CompletionCount);
}
