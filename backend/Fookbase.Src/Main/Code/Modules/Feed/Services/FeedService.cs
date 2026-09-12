using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fookbase.Api.Modules.Feed.Config;
using Fookbase.Api.Modules.Feed.DTOs.Responses;
using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Groups.Services;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Pages.Services;
using Fookbase.Api.Modules.Posts.DTOs.Responses;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Modules.Reels.DTOs.Responses;
using Fookbase.Api.Modules.Reels.Services;
using Fookbase.Api.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Feed.Services;

public sealed class FeedService(
    FookbaseDbContext dbContext,
    FriendsService friendsService,
    GroupPostAccessService groupPostAccessService,
    PagePostAccessService pagePostAccessService,
    PostsService postsService,
    FeedRankingOptions options,
    IDataProtectionProvider protectionProvider,
    TimeProvider timeProvider)
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 50;
    private const int CursorVersion = 1;
    private readonly string rankingVersion = Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(options))));

    public Task<FeedPageResponse> GetHomeFeedAsync(
        Guid viewerUserId, string? cursorValue, int limit,
        CancellationToken cancellationToken = default) =>
        GetFeedAsync(viewerUserId, cursorValue, limit, following: false, cancellationToken);

    public Task<FeedPageResponse> GetFollowingFeedAsync(
        Guid viewerUserId, string? cursorValue, int limit,
        CancellationToken cancellationToken = default) =>
        GetFeedAsync(viewerUserId, cursorValue, limit, following: true, cancellationToken);

    private async Task<FeedPageResponse> GetFeedAsync(
        Guid viewerUserId, string? cursorValue, int limit, bool following,
        CancellationToken cancellationToken)
    {
        if (limit is < 1 or > MaximumPageSize)
        {
            throw new FormatException("The feed limit is invalid.");
        }

        var protector = protectionProvider.CreateProtector(
            "Fookbase.Feed", CursorVersion.ToString(), viewerUserId.ToString("N"),
            following ? "following" : "home", rankingVersion);
        var session = DecodeCursor(cursorValue, protector);
        // Eligibility is rebuilt for EVERY request, including requests carrying an old cursor.
        var relationships = await friendsService.GetAccessSnapshotAsync(viewerUserId, cancellationToken);
        var viewer = new PostViewerContext(viewerUserId, relationships.FriendUserIds, relationships.BlockedUserIds);
        var posts = dbContext.Posts.AsNoTracking().Where(post => post.CreatedAtUtc <= session.AsOfUtc);
        var profile = PostVisibility.ApplyHomeFeed(posts, viewer)
            .Where(post => post.PostType == PostType.Standard ||
                ReelMediaQuery.ApplyReadyMedia(dbContext.Posts, dbContext).Any(reel => reel.Id == post.Id));
        var groups = groupPostAccessService.ApplyDirectAccess(posts, viewer)
            .Where(post => post.PostType == PostType.Standard &&
                dbContext.GroupMembers.Any(member => member.GroupId == post.ContainerId && member.UserId == viewerUserId));
        var pages = pagePostAccessService.ApplyPublishedAccess(posts)
            .Where(post => post.PostType == PostType.Standard &&
                dbContext.PageFollowers.Any(follower => follower.PageId == post.ContainerId && follower.UserId == viewerUserId));

        var organic = new List<Candidate>();
        await AddWindowAsync(profile.Where(post => post.AuthorUserId == viewerUserId), options.OwnAffinity);
        await AddWindowAsync(profile.Where(post => post.AuthorUserId != viewerUserId), options.FriendAffinity);
        await AddWindowAsync(groups, options.GroupAffinity);
        await AddWindowAsync(pages, options.PageAffinity);
        organic = organic.OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Post.CreatedAtUtc).ThenByDescending(item => item.Post.Id).ToList();

        var suggestions = new List<Candidate>();
        if (!following && (organic.Count > 0 || session.OrganicSinceSuggestion >= options.OrganicItemsPerSuggestion))
        {
            var oldest = session.AsOfUtc.AddDays(-options.SuggestedReelMaxAgeDays);
            var discoverable = ReelMediaQuery.ApplyReadyMedia(
                PostVisibility.ApplyDirectAccess(posts, viewer), dbContext)
                .Where(post => post.PostType == PostType.Reel && post.Privacy == PostPrivacy.Public &&
                    post.AuthorUserId != viewerUserId && !viewer.FriendUserIds.Contains(post.AuthorUserId) &&
                    post.CreatedAtUtc >= oldest);
            suggestions = await LoadWindowAsync(discoverable, options.SuggestedReelAffinity,
                session.Suggestion, session.AsOfUtc, true, cancellationToken);
        }

        // Organic score order is stable. Discovery uses its own frontier so quota-deferred
        // reels are not accidentally discarded by the organic keyset. Credit spans pages.
        var selection = new List<(Candidate Item, FeedCursor Cursor)>();
        var organicIndex = 0;
        var suggestionIndex = 0;
        var state = session;
        while (selection.Count <= limit)
        {
            Candidate candidate;
            if (!following && state.OrganicSinceSuggestion >= options.OrganicItemsPerSuggestion &&
                suggestionIndex < suggestions.Count)
            {
                candidate = suggestions[suggestionIndex++];
                state = state with { Suggestion = Position(candidate), OrganicSinceSuggestion = 0 };
            }
            else if (organicIndex < organic.Count)
            {
                candidate = organic[organicIndex++];
                state = state with
                {
                    Organic = Position(candidate),
                    OrganicSinceSuggestion = following ? 0 :
                        Math.Min(options.OrganicItemsPerSuggestion, state.OrganicSinceSuggestion + 1)
                };
            }
            else
            {
                break;
            }

            selection.Add((candidate, state));
        }

        var visible = selection.Take(limit).ToList();
        return new FeedPageResponse(
            await BuildItemsAsync(visible.Select(row => row.Item).ToList(), viewerUserId, cancellationToken),
            selection.Count > limit ? protector.Protect(JsonSerializer.Serialize(visible[^1].Cursor)) : null,
            session.AsOfUtc);

        async Task AddWindowAsync(IQueryable<Post> query, int affinity)
        {
            organic.AddRange(await LoadWindowAsync(query, following ? 0 : affinity,
                session.Organic, session.AsOfUtc, false, cancellationToken));
        }
    }

    private async Task<List<Candidate>> LoadWindowAsync(
        IQueryable<Post> query, int affinity, FeedPosition? position, DateTimeOffset asOfUtc,
        bool suggested, CancellationToken cancellationToken)
    {
        var affinityTicks = (long)affinity * options.FreshnessHoursPerPoint * TimeSpan.TicksPerHour;
        if (position is not null)
        {
            // For one source, affinity is constant: convert the score frontier back into
            // a CreatedAt predicate so PostgreSQL can use the existing chronological indexes.
            var boundaryTicks = asOfUtc.Ticks + position.Score - affinityTicks;
            if (boundaryTicks < DateTimeOffset.MinValue.Ticks)
            {
                return [];
            }

            if (boundaryTicks <= DateTimeOffset.MaxValue.Ticks)
            {
                var boundary = new DateTimeOffset(boundaryTicks, TimeSpan.Zero);
                query = query.Where(post => post.CreatedAtUtc < boundary ||
                    post.CreatedAtUtc == boundary &&
                    (post.CreatedAtUtc < position.CreatedAtUtc ||
                     post.CreatedAtUtc == position.CreatedAtUtc && post.Id.CompareTo(position.Id) < 0));
            }
        }

        // Keyset filtering happens BEFORE the bounded window; later pages can therefore
        // traverse arbitrarily far beyond the first window without retaining a timeline.
        var posts = await query.OrderByDescending(post => post.CreatedAtUtc).ThenByDescending(post => post.Id)
            .Take(options.CandidateLimitPerSource).ToListAsync(cancellationToken);
        return posts.Select(post => new Candidate(post,
            affinityTicks - (asOfUtc.Ticks - post.CreatedAtUtc.Ticks), suggested)).ToList();
    }

    private FeedCursor DecodeCursor(string? value, IDataProtector protector)
    {
        var now = timeProvider.GetUtcNow();
        if (value is null)
        {
            // PostgreSQL timestamps have microsecond precision.
            return new FeedCursor(CursorVersion, now.AddTicks(-(now.Ticks % 10)), null, null, 0);
        }

        try
        {
            if (value.Length is < 1 or > 8192)
            {
                throw new FormatException("The feed cursor is invalid.");
            }

            var cursor = JsonSerializer.Deserialize<FeedCursor>(protector.Unprotect(value));
            if (cursor is null || cursor.Version != CursorVersion || cursor.AsOfUtc.Offset != TimeSpan.Zero ||
                cursor.AsOfUtc > now || cursor.AsOfUtc < DateTimeOffset.UnixEpoch ||
                cursor.OrganicSinceSuggestion < 0 || cursor.OrganicSinceSuggestion > options.OrganicItemsPerSuggestion ||
                cursor.Organic is null || !ValidPosition(cursor.Organic, cursor.AsOfUtc) ||
                cursor.Suggestion is not null && !ValidPosition(cursor.Suggestion, cursor.AsOfUtc))
            {
                throw new FormatException("The feed cursor is invalid.");
            }

            return cursor;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or ArgumentException)
        {
            throw new FormatException("The feed cursor is invalid or belongs to a different feed session.", exception);
        }
    }

    private static bool ValidPosition(FeedPosition position, DateTimeOffset asOfUtc) =>
        position.Id != Guid.Empty && position.CreatedAtUtc.Offset == TimeSpan.Zero &&
        position.CreatedAtUtc <= asOfUtc &&
        position.Score >= -DateTimeOffset.MaxValue.Ticks && position.Score <= DateTimeOffset.MaxValue.Ticks;

    private static FeedPosition Position(Candidate candidate) =>
        new(candidate.Score, candidate.Post.CreatedAtUtc, candidate.Post.Id);

    private async Task<IReadOnlyList<FeedItemResponse>> BuildItemsAsync(
        IReadOnlyList<Candidate> candidates, Guid viewerUserId, CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var posts = candidates.Select(candidate => candidate.Post).ToList();
        var summaries = (await postsService.LoadResponsesAsync(posts, viewerUserId, cancellationToken))
            .ToDictionary(post => post.Id);
        var postIds = posts.Select(post => post.Id).ToArray();
        var authorIds = posts.Where(post => post.ContainerType != PostContainerType.Page)
            .Select(post => post.AuthorUserId).Distinct().ToArray();
        var profiles = await dbContext.UserProfiles.AsNoTracking().Where(profile => authorIds.Contains(profile.UserId))
            .Select(profile => new { profile.UserId, profile.Username, profile.DisplayName, profile.AvatarUrl, profile.AvatarMediaId })
            .ToDictionaryAsync(profile => profile.UserId, cancellationToken);
        var groupIds = posts.Where(post => post.ContainerType == PostContainerType.Group)
            .Select(post => post.ContainerId).Distinct().ToArray();
        var groups = await dbContext.Groups.AsNoTracking()
            .Where(group => groupIds.Contains(group.Id) && group.DeletedAtUtc == null)
            .Select(group => new { group.Id, group.Name, group.Privacy })
            .ToDictionaryAsync(group => group.Id, cancellationToken);
        var media = await (
            from attachment in dbContext.PostMedia.AsNoTracking()
            join asset in dbContext.MediaAssets.AsNoTracking() on attachment.MediaId equals asset.Id
            where postIds.Contains(attachment.PostId) && asset.Status == MediaStatus.Ready && asset.DeletedAtUtc == null
            orderby attachment.PostId, attachment.SortOrder
            select new { attachment.PostId, Asset = asset })
            .ToListAsync(cancellationToken);
        var mediaByPost = media.ToLookup(row => row.PostId, row => row.Asset);
        var results = new List<FeedItemResponse>();
        foreach (var candidate in candidates)
        {
            var post = candidate.Post;
            var summary = summaries[post.Id];
            var profile = profiles.GetValueOrDefault(post.AuthorUserId);
            PostDisplayIdentityResponse displayAuthor;
            FeedContainerResponse container;
            if (post.ContainerType == PostContainerType.Page)
            {
                // Fail closed if the Page disappeared between candidate and DTO queries.
                if (summary.DisplayAuthor is not { Type: "page" } pageAuthor) continue;
                displayAuthor = pageAuthor;
                container = new(post.ContainerId, pageAuthor.Name, pageAuthor.Username, null);
            }
            else
            {
                var username = profile?.Username ?? post.AuthorUserId.ToString("N");
                displayAuthor = new("user", post.AuthorUserId, username, profile?.DisplayName ?? username,
                    profile?.AvatarMediaId is null ? profile?.AvatarUrl : $"/api/users/{post.AuthorUserId}/avatar");
                if (post.ContainerType == PostContainerType.Group)
                {
                    var group = groups.GetValueOrDefault(post.ContainerId);
                    if (group is null) continue;
                    container = new(group.Id, group.Name, null, group.Privacy.ToString().ToLowerInvariant());
                }
                else
                {
                    container = new(post.ContainerId, displayAuthor.Name, username, summary.Privacy);
                }
            }

            var assets = mediaByPost[post.Id].ToList();
            ReelVideoResponse? video = null;
            if (post.PostType == PostType.Reel)
            {
                var asset = assets.FirstOrDefault(asset => asset.MediaType == MediaType.Video &&
                    asset.ProcessedObjectKey != null && asset.PosterObjectKey != null &&
                    asset.DurationMs != null && asset.Width != null && asset.Height != null);
                if (asset is null) continue;
                video = new(asset.Id, asset.DurationMs!.Value, asset.Width!.Value, asset.Height!.Value,
                    "video/mp4", $"/api/reels/{post.Id}/video/access", $"/api/reels/{post.Id}/poster/access");
            }

            results.Add(new FeedItemResponse(post.Id, post.Content, summary.Privacy, post.CreatedAtUtc, post.UpdatedAtUtc,
                new FeedAuthorResponse(post.ContainerType == PostContainerType.Page ? null : post.AuthorUserId,
                    displayAuthor.Username, displayAuthor.Name, displayAuthor.AvatarUrl),
                assets.Select(asset => asset.Id).ToList(),
                assets.Select(asset => new FeedMediaResponse(asset.Id, asset.MediaType.ToString().ToLowerInvariant(), asset.ContentType)).ToList(),
                summary.CommentCount, summary.ReactionCounts.Values.Sum(), summary.ReactionCounts, summary.ViewerReaction,
                post.PostType == PostType.Reel ? "reel" : "standardPost", post.ContainerType.ToString().ToLowerInvariant(),
                container, displayAuthor, video, candidate.IsSuggested));
        }

        return results;
    }

    private sealed record Candidate(Post Post, long Score, bool IsSuggested);
    private sealed record FeedPosition(long Score, DateTimeOffset CreatedAtUtc, Guid Id);
    private sealed record FeedCursor(int Version, DateTimeOffset AsOfUtc, FeedPosition? Organic,
        FeedPosition? Suggestion, int OrganicSinceSuggestion);
}
