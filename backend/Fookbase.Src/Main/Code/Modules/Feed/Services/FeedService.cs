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
using Fookbase.Api.Modules.Reels.Entities;
using Fookbase.Api.Modules.Reels.Services;
using Fookbase.Api.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using PublicProfileHandle = Fookbase.Api.Modules.Users.Common.PublicProfileHandle;

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
    private const int CursorVersion = 2;
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
        var profile = PostVisibility.ApplyDirectAccess(posts, viewer)
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
        await AddWindowAsync(profile.Where(post =>
            relationships.FollowedUserIds.Contains(post.AuthorUserId) &&
            viewer.FriendUserIds.Contains(post.AuthorUserId)), options.FriendAffinity);
        await AddWindowAsync(profile.Where(post =>
            relationships.FollowedUserIds.Contains(post.AuthorUserId) &&
            !viewer.FriendUserIds.Contains(post.AuthorUserId) &&
            post.Privacy == PostPrivacy.Public), options.FollowedNonFriendProfile);
        await AddWindowAsync(groups, options.GroupAffinity);
        await AddWindowAsync(pages, options.PageAffinity);
        var shares = dbContext.PostShares.AsNoTracking().Where(share =>
            share.DeletedAtUtc == null && share.CreatedAtUtc <= session.AsOfUtc);
        await AddShareWindowAsync(shares.Where(share =>
            share.DestinationType == PostShareDestinationType.Profile &&
            share.DestinationId == viewerUserId), options.OwnAffinity);
        await AddShareWindowAsync(shares.Where(share =>
            share.DestinationType == PostShareDestinationType.Profile &&
            share.SharingUserId != viewerUserId &&
            viewer.FriendUserIds.Contains(share.SharingUserId) &&
            !viewer.BlockedUserIds.Contains(share.SharingUserId)), options.FriendAffinity);
        await AddShareWindowAsync(shares.Where(share =>
            share.DestinationType == PostShareDestinationType.Group &&
            dbContext.GroupMembers.Any(member =>
                member.GroupId == share.DestinationId && member.UserId == viewerUserId)), options.GroupAffinity);
        await AddShareWindowAsync(shares.Where(share =>
            share.DestinationType == PostShareDestinationType.Page &&
            dbContext.PageFollowers.Any(follower =>
                follower.PageId == share.DestinationId && follower.UserId == viewerUserId)), options.PageAffinity);
        organic = (await RankCandidatesAsync(organic, viewerUserId, session.AsOfUtc, cancellationToken))
            .Where(candidate => session.Organic is null || IsAfterPosition(candidate, session.Organic))
            .ToList();
        organic = organic.OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.CreatedAtUtc).ThenByDescending(item => item.SortId).ToList();

        var suggestions = new List<Candidate>();
        if (!following && (organic.Count > 0 || session.OrganicSinceSuggestion >= options.OrganicItemsPerSuggestion))
        {
            var oldest = session.AsOfUtc.AddDays(-options.SuggestedReelMaxAgeDays);
            var discoverable = ReelMediaQuery.ApplyReadyMedia(
                PostVisibility.ApplyDirectAccess(posts, viewer), dbContext)
                .Where(post => post.PostType == PostType.Reel && post.Privacy == PostPrivacy.Public &&
                    post.AuthorUserId != viewerUserId && !relationships.FollowedUserIds.Contains(post.AuthorUserId) &&
                    post.CreatedAtUtc >= oldest);
            suggestions = await LoadWindowAsync(discoverable, options.SuggestedReelAffinity,
                null, session.AsOfUtc, true, cancellationToken);
            var suggestedPosts = PostVisibility.ApplyDirectAccess(posts, viewer)
                .Where(post => post.PostType == PostType.Standard && post.Privacy == PostPrivacy.Public &&
                    post.ContainerType == PostContainerType.Profile && post.AuthorUserId != viewerUserId &&
                    !relationships.FollowedUserIds.Contains(post.AuthorUserId));
            suggestions.AddRange(await LoadWindowAsync(suggestedPosts, options.SuggestedReelAffinity,
                null, session.AsOfUtc, true, cancellationToken));
            suggestions = (await RankCandidatesAsync(suggestions, viewerUserId, session.AsOfUtc, cancellationToken))
                .Where(candidate => session.Suggestion is null || IsAfterPosition(candidate, session.Suggestion))
                .Where(candidate => candidate.Post.AuthorUserId != session.LastSuggestedCreatorId)
                .OrderByDescending(candidate => candidate.Score).ThenByDescending(candidate => candidate.CreatedAtUtc)
                .ThenByDescending(candidate => candidate.SortId).ToList();
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
                while (suggestionIndex < suggestions.Count &&
                       suggestions[suggestionIndex].Post.AuthorUserId == state.LastSuggestedCreatorId)
                {
                    suggestionIndex++;
                }

                if (suggestionIndex < suggestions.Count)
                {
                    candidate = suggestions[suggestionIndex++];
                    state = state with
                    {
                        Suggestion = Position(candidate),
                        OrganicSinceSuggestion = 0,
                        LastSuggestedCreatorId = candidate.Post.AuthorUserId
                    };
                }
                else if (organicIndex < organic.Count)
                {
                    candidate = organic[organicIndex++];
                    state = state with
                    {
                        Organic = Position(candidate),
                        OrganicSinceSuggestion = Math.Min(options.OrganicItemsPerSuggestion, state.OrganicSinceSuggestion + 1)
                    };
                }
                else
                {
                    break;
                }
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

        async Task AddShareWindowAsync(IQueryable<PostShare> query, int affinity)
        {
            organic.AddRange(await LoadShareWindowAsync(query, following ? 0 : affinity,
                session.Organic, session.AsOfUtc, viewer, cancellationToken));
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
        return posts.Select(post => new Candidate(
            post,
            null,
            post.CreatedAtUtc,
            post.Id,
            affinityTicks - (asOfUtc.Ticks - post.CreatedAtUtc.Ticks),
            suggested,
            affinity)).ToList();
    }

    private async Task<List<Candidate>> LoadShareWindowAsync(
        IQueryable<PostShare> shares,
        int affinity,
        FeedPosition? position,
        DateTimeOffset asOfUtc,
        PostViewerContext viewer,
        CancellationToken cancellationToken)
    {
        var affinityTicks = (long)affinity * options.FreshnessHoursPerPoint * TimeSpan.TicksPerHour;
        var candidates = new List<Candidate>();
        var scanPosition = position;
        while (candidates.Count < options.CandidateLimitPerSource)
        {
            var window = shares;
            if (scanPosition is not null)
            {
                var boundaryTicks = asOfUtc.Ticks + scanPosition.Score - affinityTicks;
                if (boundaryTicks < DateTimeOffset.MinValue.Ticks)
                {
                    break;
                }

                var boundary = new DateTimeOffset(Math.Min(boundaryTicks, DateTimeOffset.MaxValue.Ticks), TimeSpan.Zero);
                window = window.Where(share => share.CreatedAtUtc < boundary ||
                    share.CreatedAtUtc == boundary && share.Id.CompareTo(scanPosition.Id) < 0);
            }

            var rows = await (
                from share in window
                join post in dbContext.Posts.AsNoTracking() on share.OriginalPostId equals post.Id
                where post.DeletedAtUtc == null
                orderby share.CreatedAtUtc descending, share.Id descending
                select new { Share = share, Post = post })
                .Take(options.CandidateLimitPerSource * 2)
                .ToListAsync(cancellationToken);
            if (rows.Count == 0)
            {
                break;
            }

            foreach (var row in rows)
            {
                scanPosition = new FeedPosition(
                    affinityTicks - (asOfUtc.Ticks - row.Share.CreatedAtUtc.Ticks),
                    row.Share.CreatedAtUtc,
                    row.Share.Id);
                if (!await postsService.CanViewPostAsync(row.Post, viewer, cancellationToken))
                {
                    continue;
                }

                candidates.Add(new Candidate(
                    row.Post,
                    row.Share,
                    row.Share.CreatedAtUtc,
                    row.Share.Id,
                    affinityTicks - (asOfUtc.Ticks - row.Share.CreatedAtUtc.Ticks),
                    false,
                    affinity));
                if (candidates.Count >= options.CandidateLimitPerSource)
                {
                    break;
                }
            }

            if (rows.Count < options.CandidateLimitPerSource * 2)
            {
                break;
            }
        }

        return candidates;
    }

    // All interaction queries are grouped in PostgreSQL and constrained to candidate authors/sources,
    // the bounded lookback window, and the cursor snapshot. No per-candidate query is issued here.
    private async Task<List<Candidate>> RankCandidatesAsync(
        List<Candidate> candidates,
        Guid viewerUserId,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return candidates;
        }

        var candidatePostIds = candidates.Select(candidate => candidate.Post.Id).Distinct().ToArray();
        var creatorIds = candidates.Where(candidate => candidate.Post.ContainerType == PostContainerType.Profile)
            .Select(candidate => candidate.Post.AuthorUserId).Distinct().ToArray();
        var sourceKeys = candidates.Where(candidate => candidate.Post.ContainerType is PostContainerType.Group or PostContainerType.Page)
            .Select(candidate => new SourceKey(candidate.Post.ContainerType, candidate.Post.ContainerId)).Distinct().ToArray();
        var sourceIds = sourceKeys.Select(key => key.Id).Distinct().ToArray();
        var lookbackStart = asOfUtc.AddDays(-options.InteractionLookbackDays);
        var creatorSignals = new Dictionary<Guid, int>();
        var sourceSignals = new Dictionary<SourceKey, int>();

        var reactions = await (
            from reaction in dbContext.PostReactions.AsNoTracking()
            join post in dbContext.Posts.AsNoTracking() on reaction.PostId equals post.Id
            where reaction.UserId == viewerUserId && reaction.CreatedAtUtc >= lookbackStart && reaction.CreatedAtUtc <= asOfUtc &&
                  (creatorIds.Contains(post.AuthorUserId) || sourceIds.Contains(post.ContainerId))
            group reaction by new { post.AuthorUserId, post.ContainerType, post.ContainerId } into grouped
            select new InteractionAggregate(grouped.Key.AuthorUserId, grouped.Key.ContainerType, grouped.Key.ContainerId, grouped.Count()))
            .ToListAsync(cancellationToken);
        AddSignals(reactions, options.ReactionAffinityWeight, creatorSignals, sourceSignals, creatorIds, sourceKeys);

        var comments = await (
            from comment in dbContext.Comments.AsNoTracking()
            join post in dbContext.Posts.AsNoTracking() on comment.PostId equals post.Id
            where comment.AuthorUserId == viewerUserId && comment.DeletedAtUtc == null &&
                  comment.CreatedAtUtc >= lookbackStart && comment.CreatedAtUtc <= asOfUtc &&
                  (creatorIds.Contains(post.AuthorUserId) || sourceIds.Contains(post.ContainerId))
            group comment by new { post.AuthorUserId, post.ContainerType, post.ContainerId } into grouped
            select new InteractionAggregate(grouped.Key.AuthorUserId, grouped.Key.ContainerType, grouped.Key.ContainerId, grouped.Count()))
            .ToListAsync(cancellationToken);
        AddSignals(comments, options.CommentAffinityWeight, creatorSignals, sourceSignals, creatorIds, sourceKeys);

        var saves = await (
            from save in dbContext.PostSaves.AsNoTracking()
            join post in dbContext.Posts.AsNoTracking() on save.PostId equals post.Id
            where save.UserId == viewerUserId && save.SavedAtUtc >= lookbackStart && save.SavedAtUtc <= asOfUtc &&
                  (creatorIds.Contains(post.AuthorUserId) || sourceIds.Contains(post.ContainerId))
            group save by new { post.AuthorUserId, post.ContainerType, post.ContainerId } into grouped
            select new InteractionAggregate(grouped.Key.AuthorUserId, grouped.Key.ContainerType, grouped.Key.ContainerId, grouped.Count()))
            .ToListAsync(cancellationToken);
        AddSignals(saves, options.SaveAffinityWeight, creatorSignals, sourceSignals, creatorIds, sourceKeys);

        var shares = await (
            from share in dbContext.PostShares.AsNoTracking()
            join post in dbContext.Posts.AsNoTracking() on share.OriginalPostId equals post.Id
            where share.SharingUserId == viewerUserId && share.DeletedAtUtc == null &&
                  share.CreatedAtUtc >= lookbackStart && share.CreatedAtUtc <= asOfUtc &&
                  (creatorIds.Contains(post.AuthorUserId) || sourceIds.Contains(post.ContainerId))
            group share by new { post.AuthorUserId, post.ContainerType, post.ContainerId } into grouped
            select new InteractionAggregate(grouped.Key.AuthorUserId, grouped.Key.ContainerType, grouped.Key.ContainerId, grouped.Count()))
            .ToListAsync(cancellationToken);
        AddSignals(shares, options.ShareAffinityWeight, creatorSignals, sourceSignals, creatorIds, sourceKeys);

        var sourcePosts = await dbContext.Posts.AsNoTracking()
            .Where(post => post.AuthorUserId == viewerUserId && post.CreatedAtUtc >= lookbackStart && post.CreatedAtUtc <= asOfUtc &&
                sourceIds.Contains(post.ContainerId) && (post.ContainerType == PostContainerType.Group || post.ContainerType == PostContainerType.Page))
            .GroupBy(post => new { post.ContainerType, post.ContainerId })
            .Select(group => new SourceAggregate(group.Key.ContainerType, group.Key.ContainerId, group.Count()))
            .ToListAsync(cancellationToken);
        foreach (var sourcePost in sourcePosts)
        {
            var key = new SourceKey(sourcePost.ContainerType, sourcePost.ContainerId);
            if (sourceKeys.Contains(key)) Add(sourceSignals, key, sourcePost.Count * options.CommentAffinityWeight);
        }

        var watches = await (
            from view in dbContext.ReelViews.AsNoTracking()
            join post in dbContext.Posts.AsNoTracking() on view.ReelPostId equals post.Id
            join attachment in dbContext.PostMedia.AsNoTracking() on post.Id equals attachment.PostId
            join media in dbContext.MediaAssets.AsNoTracking() on attachment.MediaId equals media.Id
            where view.ViewerUserId == viewerUserId && view.ViewedAtUtc >= lookbackStart && view.ViewedAtUtc <= asOfUtc &&
                  creatorIds.Contains(post.AuthorUserId) && media.DurationMs != null
            group new { view, media } by post.AuthorUserId into grouped
            select new WatchAggregate(
                grouped.Key,
                grouped.Count(item => item.view.Completed || item.view.WatchDurationMs * 100 >= item.media.DurationMs!.Value * options.ReelStrongCompletionThreshold),
                grouped.Count(item => !item.view.Completed && item.view.WatchDurationMs * 100 <= item.media.DurationMs!.Value * 20)))
            .ToListAsync(cancellationToken);
        var watchSignals = watches.ToDictionary(
            watch => watch.AuthorUserId,
            watch => Math.Clamp(
                watch.StrongCount * options.ReelCompletionWeight - watch.EarlyExitCount * options.ReelEarlyExitPenalty,
                -options.ReelWatchAffinityCap,
                options.ReelWatchAffinityCap));

        var reactionEngagement = await dbContext.PostReactions.AsNoTracking()
            .Where(reaction => candidatePostIds.Contains(reaction.PostId) && reaction.CreatedAtUtc <= asOfUtc)
            .GroupBy(reaction => reaction.PostId).Select(group => new PostCount(group.Key, group.Count()))
            .ToDictionaryAsync(row => row.PostId, row => row.Count, cancellationToken);
        var commentEngagement = await dbContext.Comments.AsNoTracking()
            .Where(comment => candidatePostIds.Contains(comment.PostId) && comment.DeletedAtUtc == null && comment.CreatedAtUtc <= asOfUtc)
            .GroupBy(comment => comment.PostId).Select(group => new PostCount(group.Key, group.Count()))
            .ToDictionaryAsync(row => row.PostId, row => row.Count, cancellationToken);
        var shareEngagement = await dbContext.PostShares.AsNoTracking()
            .Where(share => candidatePostIds.Contains(share.OriginalPostId) && share.DeletedAtUtc == null && share.CreatedAtUtc <= asOfUtc)
            .GroupBy(share => share.OriginalPostId).Select(group => new PostCount(group.Key, group.Count()))
            .ToDictionaryAsync(row => row.PostId, row => row.Count, cancellationToken);

        var scoreTicksPerPoint = (long)options.FreshnessHoursPerPoint * TimeSpan.TicksPerHour;
        return candidates.Select(candidate =>
        {
            var post = candidate.Post;
            var creatorAffinity = post.ContainerType == PostContainerType.Profile
                ? Math.Min(options.CreatorAffinityCap, creatorSignals.GetValueOrDefault(post.AuthorUserId))
                : 0;
            var sourceAffinity = post.ContainerType is PostContainerType.Group or PostContainerType.Page
                ? Math.Min(options.SourceAffinityCap, sourceSignals.GetValueOrDefault(new SourceKey(post.ContainerType, post.ContainerId)))
                : 0;
            var watchAffinity = post.ContainerType == PostContainerType.Profile
                ? watchSignals.GetValueOrDefault(post.AuthorUserId)
                : 0;
            var engagement = Math.Min(options.EngagementNormalizationCap, reactionEngagement.GetValueOrDefault(post.Id)) * options.ReactionEngagementWeight +
                Math.Min(options.EngagementNormalizationCap, commentEngagement.GetValueOrDefault(post.Id)) * options.CommentEngagementWeight +
                Math.Min(options.EngagementNormalizationCap, shareEngagement.GetValueOrDefault(post.Id)) * options.ShareEngagementWeight;
            var score = (candidate.BaseAffinity + creatorAffinity + sourceAffinity + watchAffinity) * scoreTicksPerPoint +
                (long)engagement * options.EngagementMinutesPerPoint * TimeSpan.TicksPerMinute -
                (asOfUtc.Ticks - candidate.CreatedAtUtc.Ticks);
            return candidate with { Score = score };
        }).ToList();
    }

    private static void AddSignals(
        IReadOnlyList<InteractionAggregate> aggregates,
        int weight,
        Dictionary<Guid, int> creatorSignals,
        Dictionary<SourceKey, int> sourceSignals,
        IReadOnlyCollection<Guid> creatorIds,
        IReadOnlyCollection<SourceKey> sourceKeys)
    {
        foreach (var aggregate in aggregates)
        {
            var amount = aggregate.Count * weight;
            if (creatorIds.Contains(aggregate.AuthorUserId)) Add(creatorSignals, aggregate.AuthorUserId, amount);
            var source = new SourceKey(aggregate.ContainerType, aggregate.ContainerId);
            if (sourceKeys.Contains(source)) Add(sourceSignals, source, amount);
        }
    }

    private static void Add<TKey>(Dictionary<TKey, int> values, TKey key, int amount) where TKey : notnull =>
        values[key] = values.GetValueOrDefault(key) + amount;

    private FeedCursor DecodeCursor(string? value, IDataProtector protector)
    {
        var now = timeProvider.GetUtcNow();
        if (value is null)
        {
            // PostgreSQL timestamps have microsecond precision.
            return new FeedCursor(CursorVersion, now.AddTicks(-(now.Ticks % 10)), null, null, 0, null);
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
        new(candidate.Score, candidate.CreatedAtUtc, candidate.SortId);

    private static bool IsAfterPosition(Candidate candidate, FeedPosition position) =>
        candidate.Score < position.Score ||
        candidate.Score == position.Score &&
        (candidate.CreatedAtUtc < position.CreatedAtUtc ||
         candidate.CreatedAtUtc == position.CreatedAtUtc && candidate.SortId.CompareTo(position.Id) < 0);

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
        var authorIds = candidates
            .SelectMany(candidate =>
            {
                if (candidate.Share is not null)
                {
                    return new[] { candidate.Share.SharingUserId };
                }

                return candidate.Post.ContainerType == PostContainerType.Page
                    ? Array.Empty<Guid>()
                    : new[] { candidate.Post.AuthorUserId };
            })
            .Distinct()
            .ToArray();
        var profiles = await dbContext.UserProfiles.AsNoTracking().Where(profile => authorIds.Contains(profile.UserId))
            .Select(profile => new { profile.UserId, profile.Username, profile.DisplayName, profile.AvatarUrl, profile.AvatarMediaId })
            .ToDictionaryAsync(profile => profile.UserId, cancellationToken);
        var groupIds = candidates.SelectMany(candidate =>
            (candidate.Post.ContainerType == PostContainerType.Group
                ? new[] { candidate.Post.ContainerId }
                : Array.Empty<Guid>())
            .Concat(candidate.Share?.DestinationType == PostShareDestinationType.Group
                ? new[] { candidate.Share.DestinationId }
                : Array.Empty<Guid>()))
            .Distinct()
            .ToArray();
        var groups = await dbContext.Groups.AsNoTracking()
            .Where(group => groupIds.Contains(group.Id) && group.DeletedAtUtc == null)
            .Select(group => new { group.Id, group.Name, group.Privacy })
            .ToDictionaryAsync(group => group.Id, cancellationToken);
        var destinationPageIds = candidates
            .Where(candidate => candidate.Share?.DestinationType == PostShareDestinationType.Page)
            .Select(candidate => candidate.Share!.DestinationId)
            .Distinct()
            .ToArray();
        var destinationPages = destinationPageIds.Length == 0
            ? new Dictionary<Guid, PageDestinationIdentity>()
            : await dbContext.Pages.AsNoTracking()
                .Where(page => destinationPageIds.Contains(page.Id) && page.DeletedAtUtc == null)
                .Select(page => new PageDestinationIdentity(page.Id, page.Username, page.Name, page.AvatarMediaId))
                .ToDictionaryAsync(page => page.Id, cancellationToken);
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
                var username = PublicProfileHandle.From(profile?.Username ?? string.Empty);
                var displayName = PublicProfileHandle.From(profile?.DisplayName ?? string.Empty);
                displayAuthor = new("user", post.AuthorUserId, username,
                    string.IsNullOrWhiteSpace(displayName) ? (string.IsNullOrWhiteSpace(username) ? "Người dùng" : username) : displayName,
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

            var itemId = post.Id;
            var content = post.Content;
            var createdAtUtc = post.CreatedAtUtc;
            var updatedAtUtc = post.UpdatedAtUtc;
            var contentType = post.PostType == PostType.Reel ? "reel" : "standardPost";
            var containerType = post.ContainerType.ToString().ToLowerInvariant();
            var author = new FeedAuthorResponse(post.ContainerType == PostContainerType.Page ? null : post.AuthorUserId,
                displayAuthor.Username, displayAuthor.Name, displayAuthor.AvatarUrl);
            FeedShareResponse? shareResponse = null;
            if (candidate.Share is { } share)
            {
                var actorProfile = profiles.GetValueOrDefault(share.SharingUserId);
                var actorUsername = PublicProfileHandle.From(actorProfile?.Username ?? string.Empty);
                var actorDisplayName = PublicProfileHandle.From(actorProfile?.DisplayName ?? string.Empty);
                var actor = new FeedAuthorResponse(
                    share.SharingUserId,
                    actorUsername,
                    string.IsNullOrWhiteSpace(actorDisplayName)
                        ? (string.IsNullOrWhiteSpace(actorUsername) ? "Người dùng" : actorUsername)
                        : actorDisplayName,
                    actorProfile?.AvatarMediaId is null
                        ? actorProfile?.AvatarUrl
                        : $"/api/users/{share.SharingUserId}/avatar");
                switch (share.DestinationType)
                {
                    case PostShareDestinationType.Profile:
                        displayAuthor = new PostDisplayIdentityResponse(
                            "user", share.SharingUserId, actor.Username, actor.DisplayName, actor.AvatarUrl);
                        container = new(share.DestinationId, actor.DisplayName, actor.Username, "public");
                        author = actor;
                        break;
                    case PostShareDestinationType.Group:
                        var group = groups.GetValueOrDefault(share.DestinationId);
                        if (group is null) continue;
                        displayAuthor = new PostDisplayIdentityResponse(
                            "user", share.SharingUserId, actor.Username, actor.DisplayName, actor.AvatarUrl);
                        container = new(group.Id, group.Name, null, group.Privacy.ToString().ToLowerInvariant());
                        author = actor;
                        break;
                    case PostShareDestinationType.Page:
                        var page = destinationPages.GetValueOrDefault(share.DestinationId);
                        if (page is null) continue;
                        displayAuthor = new PostDisplayIdentityResponse(
                            "page", page.Id, page.Username, page.Name,
                            page.AvatarMediaId is null ? null : $"/api/pages/{page.Id}/avatar");
                        container = new(page.Id, page.Name, page.Username, null);
                        author = new FeedAuthorResponse(null, page.Username, page.Name, displayAuthor.AvatarUrl);
                        actor = author;
                        break;
                    default:
                        continue;
                }

                shareResponse = new FeedShareResponse(
                    share.Id,
                    post.Id,
                    share.Caption,
                    share.CreatedAtUtc,
                    actor,
                    post.ContainerType == PostContainerType.Page && summary.DisplayAuthor is { } pageAuthor
                        ? pageAuthor
                        : new PostDisplayIdentityResponse(
                            "user", post.AuthorUserId,
                            PublicProfileHandle.From(profile?.Username ?? string.Empty),
                            PublicProfileHandle.From(profile?.DisplayName ?? string.Empty) is { Length: > 0 } originalDisplayName
                                ? originalDisplayName
                                : PublicProfileHandle.From(profile?.Username ?? string.Empty) is { Length: > 0 } originalUsername
                                    ? originalUsername
                                    : "Người dùng",
                            profile?.AvatarMediaId is null
                                ? profile?.AvatarUrl
                                : $"/api/users/{post.AuthorUserId}/avatar"),
                    summary);
                itemId = share.Id;
                content = share.Caption ?? string.Empty;
                createdAtUtc = share.CreatedAtUtc;
                updatedAtUtc = null;
                contentType = "share";
                containerType = share.DestinationType.ToString().ToLowerInvariant();
                video = null;
                assets = [];
            }

            results.Add(new FeedItemResponse(itemId, content, summary.Privacy, createdAtUtc, updatedAtUtc,
                author,
                assets.Select(asset => asset.Id).ToList(),
                assets.Select(asset => new FeedMediaResponse(asset.Id, asset.MediaType.ToString().ToLowerInvariant(), asset.ContentType)).ToList(),
                summary.CommentCount, summary.ReactionCounts.Values.Sum(), summary.ReactionCounts, summary.ViewerReaction,
                contentType, containerType,
                container, displayAuthor, video, candidate.IsSuggested, summary.Mentions ?? [], shareResponse,
                candidate.IsSuggested ? "Suggested for you" : null,
                contentType == "standardPost" ? summary.TextBackground : null));
        }

        return results;
    }

    private sealed record Candidate(
        Post Post,
        PostShare? Share,
        DateTimeOffset CreatedAtUtc,
        Guid SortId,
        long Score,
        bool IsSuggested,
        int BaseAffinity = 0);
    private sealed record PageDestinationIdentity(Guid Id, string Username, string Name, Guid? AvatarMediaId);
    private sealed record FeedPosition(long Score, DateTimeOffset CreatedAtUtc, Guid Id);
    private sealed record FeedCursor(int Version, DateTimeOffset AsOfUtc, FeedPosition? Organic,
        FeedPosition? Suggestion, int OrganicSinceSuggestion, Guid? LastSuggestedCreatorId);
    private sealed record SourceKey(PostContainerType ContainerType, Guid Id);
    private sealed record InteractionAggregate(Guid AuthorUserId, PostContainerType ContainerType, Guid ContainerId, int Count);
    private sealed record SourceAggregate(PostContainerType ContainerType, Guid ContainerId, int Count);
    private sealed record WatchAggregate(Guid AuthorUserId, int StrongCount, int EarlyExitCount);
    private sealed record PostCount(Guid PostId, int Count);
}
