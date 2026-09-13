using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Events.Entities;
using Fookbase.Api.Modules.Friends.Entities;
using Fookbase.Api.Modules.Groups.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Pages.Entities;
using Fookbase.Api.Modules.Posts.Common;
using Fookbase.Api.Modules.Posts.DTOs.Responses;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Modules.Search.DTOs.Responses;
using Fookbase.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Search.Services;

public sealed class SearchService(
    FookbaseDbContext dbContext,
    FriendsService friendsService,
    PostsService postsService,
    SocialInteractionsService socialInteractionsService)
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 50;
    public const int DefaultSuggestionLimit = 5;

    private const int MinimumQueryLength = 2;
    private const int MaximumQueryLength = 100;
    private const int AllPreviewLimit = 5;
    private const string CursorVersion = "search-v1";

    public async Task<ApplicationResult<GlobalSearchResponse>> SearchAsync(
        Guid viewerUserId,
        string? queryValue,
        string? typeValue,
        string? cursorValue,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeQuery(queryValue, out var query, out var validationError))
        {
            return Failure<GlobalSearchResponse>(validationError!);
        }

        if (!TryParseType(typeValue, out var type))
        {
            return Validation<GlobalSearchResponse>(
                "invalid_search_type",
                "Type must be one of: all, people, groups, pages, posts, reels, events.");
        }

        if (limit is < 1 or > MaximumPageSize)
        {
            return Validation<GlobalSearchResponse>(
                "invalid_search_limit",
                $"Limit must be between 1 and {MaximumPageSize}.");
        }

        if (type == SearchType.All && !string.IsNullOrWhiteSpace(cursorValue))
        {
            return Validation<GlobalSearchResponse>(
                "invalid_search_cursor",
                "All search does not support a cursor.");
        }

        var context = await CreateContextAsync(viewerUserId, cancellationToken);
        if (type == SearchType.All)
        {
            var previewLimit = Math.Min(limit, AllPreviewLimit);
            var people = await SearchPeopleAsync(context, query, null, previewLimit, cancellationToken);
            var groups = await SearchGroupsAsync(context, query, null, previewLimit, cancellationToken);
            var pages = await SearchPagesAsync(context, query, null, previewLimit, cancellationToken);
            var posts = await SearchPostsAsync(context, query, null, previewLimit, cancellationToken);
            var reels = await SearchReelsAsync(context, query, null, previewLimit, cancellationToken);
            var events = await SearchEventsAsync(query, null, previewLimit, cancellationToken);
            var hashtags = (await socialInteractionsService.SearchHashtagsAsync(query, previewLimit, cancellationToken))
                .Select(hashtag => new SearchHashtagResponse(hashtag.NormalizedName, hashtag.DisplayName))
                .ToList();
            return ApplicationResult<GlobalSearchResponse>.Success(new(
                people.Items,
                groups.Items,
                pages.Items,
                posts.Items,
                reels.Items,
                null,
                hashtags,
                events.Items));
        }

        if (!TryDecodeCursor(cursorValue, type, query, out var cursor))
        {
            return Validation<GlobalSearchResponse>("invalid_search_cursor", "The search cursor is invalid.");
        }

        return type switch
        {
            SearchType.People => ToGlobal(await SearchPeopleAsync(context, query, cursor, limit, cancellationToken)),
            SearchType.Groups => ToGlobal(await SearchGroupsAsync(context, query, cursor, limit, cancellationToken)),
            SearchType.Pages => ToGlobal(await SearchPagesAsync(context, query, cursor, limit, cancellationToken)),
            SearchType.Posts => ToGlobal(await SearchPostsAsync(context, query, cursor, limit, cancellationToken)),
            SearchType.Reels => ToGlobal(await SearchReelsAsync(context, query, cursor, limit, cancellationToken)),
            SearchType.Events => ToGlobal(await SearchEventsAsync(query, cursor, limit, cancellationToken)),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    public async Task<ApplicationResult<SearchSuggestionsResponse>> GetSuggestionsAsync(
        Guid viewerUserId,
        string? queryValue,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeQuery(queryValue, out var query, out var validationError))
        {
            return Failure<SearchSuggestionsResponse>(validationError!);
        }

        if (limit is < 1 or > DefaultSuggestionLimit)
        {
            return Validation<SearchSuggestionsResponse>(
                "invalid_search_limit",
                $"Limit must be between 1 and {DefaultSuggestionLimit}.");
        }

        var context = await CreateContextAsync(viewerUserId, cancellationToken);
        var people = await SearchPeopleAsync(context, query, null, limit, cancellationToken);
        var groups = await SearchGroupsAsync(context, query, null, limit, cancellationToken);
        var pages = await SearchPagesAsync(context, query, null, limit, cancellationToken);
        return ApplicationResult<SearchSuggestionsResponse>.Success(new(
            people.Items,
            groups.Items,
            pages.Items));
    }

    private async Task<SearchPage<SearchPersonResponse>> SearchPeopleAsync(
        SearchContext context,
        string query,
        SearchCursor? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        var exact = query;
        var prefix = query + "%";
        var contains = "%" + query + "%";
        var blockedUserIds = context.Viewer.BlockedUserIds;
        var ranked = dbContext.UserProfiles.AsNoTracking()
            .Where(profile => !blockedUserIds.Contains(profile.UserId))
            .Where(profile => EF.Functions.ILike(profile.DisplayName, contains) ||
                              EF.Functions.ILike(profile.Username, contains))
            .Select(profile => new
            {
                profile.UserId,
                profile.Username,
                profile.DisplayName,
                profile.AvatarUrl,
                profile.AvatarMediaId,
                profile.Bio,
                FollowerCount = dbContext.UserFollows.AsNoTracking().Count(follow =>
                    follow.FollowingUserId == profile.UserId &&
                    dbContext.Users.Any(user => user.Id == follow.FollowerUserId && user.IsActive) &&
                    dbContext.UserProfiles.Any(other => other.UserId == follow.FollowerUserId) &&
                    !dbContext.BlockedUsers.AsNoTracking().Any(block =>
                        (block.BlockerUserId == follow.FollowerUserId && block.BlockedUserId == profile.UserId) ||
                        (block.BlockerUserId == profile.UserId && block.BlockedUserId == follow.FollowerUserId)) &&
                    !dbContext.BlockedUsers.AsNoTracking().Any(block =>
                        (block.BlockerUserId == context.Viewer.UserId && block.BlockedUserId == follow.FollowerUserId) ||
                        (block.BlockerUserId == follow.FollowerUserId && block.BlockedUserId == context.Viewer.UserId))),
                FollowingCount = dbContext.UserFollows.AsNoTracking().Count(follow =>
                    follow.FollowerUserId == profile.UserId &&
                    dbContext.Users.Any(user => user.Id == follow.FollowingUserId && user.IsActive) &&
                    dbContext.UserProfiles.Any(other => other.UserId == follow.FollowingUserId) &&
                    !dbContext.BlockedUsers.AsNoTracking().Any(block =>
                        (block.BlockerUserId == profile.UserId && block.BlockedUserId == follow.FollowingUserId) ||
                        (block.BlockerUserId == follow.FollowingUserId && block.BlockedUserId == profile.UserId)) &&
                    !dbContext.BlockedUsers.AsNoTracking().Any(block =>
                        (block.BlockerUserId == context.Viewer.UserId && block.BlockedUserId == follow.FollowingUserId) ||
                        (block.BlockerUserId == follow.FollowingUserId && block.BlockedUserId == context.Viewer.UserId))),
                IsFollowing = dbContext.UserFollows.AsNoTracking().Any(follow =>
                    follow.FollowerUserId == context.Viewer.UserId && follow.FollowingUserId == profile.UserId),
                IsFollowedBy = dbContext.UserFollows.AsNoTracking().Any(follow =>
                    follow.FollowerUserId == profile.UserId && follow.FollowingUserId == context.Viewer.UserId),
                FriendshipState = profile.UserId == context.Viewer.UserId ? "self" :
                    dbContext.Friendships.AsNoTracking().Any(friendship =>
                        (friendship.UserId1 == context.Viewer.UserId && friendship.UserId2 == profile.UserId) ||
                        (friendship.UserId1 == profile.UserId && friendship.UserId2 == context.Viewer.UserId)) ? "friends" :
                    dbContext.FriendRequests.AsNoTracking().Any(request =>
                        request.SenderUserId == context.Viewer.UserId && request.ReceiverUserId == profile.UserId &&
                        request.Status == FriendRequestStatus.Pending) ? "request_sent" :
                    dbContext.FriendRequests.AsNoTracking().Any(request =>
                        request.SenderUserId == profile.UserId && request.ReceiverUserId == context.Viewer.UserId &&
                        request.Status == FriendRequestStatus.Pending) ? "request_received" : "none",
                Rank = EF.Functions.ILike(profile.DisplayName, exact) || EF.Functions.ILike(profile.Username, exact)
                    ? 0
                    : EF.Functions.ILike(profile.DisplayName, prefix) || EF.Functions.ILike(profile.Username, prefix)
                        ? 1
                        : 2
            });
        if (cursor is not null)
        {
            if (cursor.Name is null)
            {
                return new([], null);
            }

            ranked = ranked.Where(item =>
                item.Rank > cursor.Rank ||
                item.Rank == cursor.Rank &&
                (string.Compare(item.DisplayName, cursor.Name) > 0 ||
                 item.DisplayName == cursor.Name && item.UserId.CompareTo(cursor.Id) > 0));
        }

        var rows = await ranked
            .OrderBy(item => item.Rank)
            .ThenBy(item => item.DisplayName)
            .ThenBy(item => item.UserId)
            .Take(limit + 1)
            .Select(item => new PersonRow(
                item.UserId,
                item.Username,
                item.DisplayName,
                item.AvatarUrl,
                item.AvatarMediaId,
                item.Bio,
                item.FollowerCount,
                item.FollowingCount,
                item.IsFollowing,
                item.IsFollowedBy,
                item.FriendshipState,
                item.Rank))
            .ToListAsync(cancellationToken);
        var visible = rows.Take(limit).ToList();
        return new(
            visible.Select(item => new SearchPersonResponse(
                item.UserId,
                item.Username,
                item.DisplayName,
                item.AvatarMediaId is null ? item.AvatarUrl : $"/api/users/{item.UserId}/avatar",
                ShortBio(item.Bio),
                item.FollowerCount,
                item.FollowingCount,
                item.IsFollowing,
                item.IsFollowedBy,
                item.FriendshipState)).ToList(),
            rows.Count > limit ? EncodeCursor(SearchType.People, query, visible[^1].Rank, visible[^1].UserId,
                name: visible[^1].DisplayName) : null);
    }

    private async Task<SearchPage<SearchGroupResponse>> SearchGroupsAsync(
        SearchContext context,
        string query,
        SearchCursor? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        var exact = query;
        var prefix = query + "%";
        var contains = "%" + query + "%";
        var viewerUserId = context.Viewer.UserId;
        var ranked = dbContext.Groups.AsNoTracking()
            .Where(group => group.DeletedAtUtc == null &&
                            (group.Privacy == GroupPrivacy.Public ||
                             dbContext.GroupMembers.Any(member =>
                                 member.GroupId == group.Id && member.UserId == viewerUserId)))
            .Where(group => EF.Functions.ILike(group.Name, contains) ||
                            group.Description != null && EF.Functions.ILike(group.Description, contains))
            .Select(group => new
            {
                GroupId = group.Id,
                group.Name,
                group.Description,
                group.Privacy,
                group.CoverMediaId,
                MemberCount = dbContext.GroupMembers.Count(member => member.GroupId == group.Id),
                IsMember = dbContext.GroupMembers.Any(member => member.GroupId == group.Id && member.UserId == viewerUserId),
                HasPendingRequest = dbContext.GroupJoinRequests.Any(request =>
                    request.GroupId == group.Id && request.RequesterUserId == viewerUserId &&
                    request.Status == GroupJoinRequestStatus.Pending),
                Rank = EF.Functions.ILike(group.Name, exact) ? 0 :
                    EF.Functions.ILike(group.Name, prefix) ? 1 : 2
            });
        if (cursor is not null)
        {
            if (cursor.Count is null)
            {
                return new([], null);
            }

            ranked = ranked.Where(item =>
                item.Rank > cursor.Rank ||
                item.Rank == cursor.Rank &&
                (item.MemberCount < cursor.Count ||
                 item.MemberCount == cursor.Count && item.GroupId.CompareTo(cursor.Id) > 0));
        }

        var rows = await ranked
            .OrderBy(item => item.Rank)
            .ThenByDescending(item => item.MemberCount)
            .ThenBy(item => item.GroupId)
            .Take(limit + 1)
            .Select(item => new GroupRow(
                item.GroupId,
                item.Name,
                item.Description,
                item.Privacy,
                item.CoverMediaId,
                item.MemberCount,
                item.IsMember,
                item.HasPendingRequest,
                item.Rank))
            .ToListAsync(cancellationToken);
        var visible = rows.Take(limit).ToList();
        return new(
            visible.Select(item => new SearchGroupResponse(
                item.GroupId,
                item.Name,
                ShortBio(item.Description),
                item.Privacy.ToString().ToLowerInvariant(),
                item.CoverMediaId is null ? null : $"/api/groups/{item.GroupId}/cover",
                item.MemberCount,
                item.IsMember ? "member" : item.HasPendingRequest ? "pending" : null)).ToList(),
            rows.Count > limit ? EncodeCursor(SearchType.Groups, query, visible[^1].Rank, visible[^1].GroupId,
                count: visible[^1].MemberCount) : null);
    }

    private async Task<SearchPage<SearchPageResponse>> SearchPagesAsync(
        SearchContext context,
        string query,
        SearchCursor? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        var exact = query;
        var prefix = query + "%";
        var contains = "%" + query + "%";
        var viewerUserId = context.Viewer.UserId;
        var ranked = dbContext.Pages.AsNoTracking()
            .Where(page => page.DeletedAtUtc == null && page.Status == PageStatus.Published)
            .Where(page => EF.Functions.ILike(page.Name, contains) ||
                           EF.Functions.ILike(page.Username, contains) ||
                           EF.Functions.ILike(page.Category, contains) ||
                           page.Bio != null && EF.Functions.ILike(page.Bio, contains))
            .Select(page => new
            {
                PageId = page.Id,
                page.Name,
                page.Username,
                page.Category,
                page.Bio,
                page.AvatarMediaId,
                FollowerCount = dbContext.PageFollowers.Count(follower => follower.PageId == page.Id),
                ViewerIsFollowing = dbContext.PageFollowers.Any(follower => follower.PageId == page.Id && follower.UserId == viewerUserId),
                Rank = EF.Functions.ILike(page.Username, exact) ? 0 :
                    EF.Functions.ILike(page.Name, exact) ? 1 :
                    EF.Functions.ILike(page.Username, prefix) ? 2 :
                    EF.Functions.ILike(page.Name, prefix) ? 3 : 4
            });
        if (cursor is not null)
        {
            if (cursor.Count is null)
            {
                return new([], null);
            }

            ranked = ranked.Where(item =>
                item.Rank > cursor.Rank ||
                item.Rank == cursor.Rank &&
                (item.FollowerCount < cursor.Count ||
                 item.FollowerCount == cursor.Count && item.PageId.CompareTo(cursor.Id) > 0));
        }

        var rows = await ranked
            .OrderBy(item => item.Rank)
            .ThenByDescending(item => item.FollowerCount)
            .ThenBy(item => item.PageId)
            .Take(limit + 1)
            .Select(item => new PageRow(
                item.PageId,
                item.Name,
                item.Username,
                item.Category,
                item.Bio,
                item.AvatarMediaId,
                item.FollowerCount,
                item.ViewerIsFollowing,
                item.Rank))
            .ToListAsync(cancellationToken);
        var visible = rows.Take(limit).ToList();
        return new(
            visible.Select(item => new SearchPageResponse(
                item.PageId,
                item.Name,
                item.Username,
                item.Category,
                ShortBio(item.Bio),
                item.AvatarMediaId is null ? null : $"/api/pages/{item.PageId}/avatar",
                item.FollowerCount,
                item.ViewerIsFollowing)).ToList(),
            rows.Count > limit ? EncodeCursor(SearchType.Pages, query, visible[^1].Rank, visible[^1].PageId,
                count: visible[^1].FollowerCount) : null);
    }

    private async Task<SearchPage<SearchPostResponse>> SearchPostsAsync(
        SearchContext context,
        string query,
        SearchCursor? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        var exact = query;
        var prefix = query + "%";
        var contains = "%" + query + "%";
        var ranked = VisibleStandardPosts(context)
            .Where(post => EF.Functions.ILike(post.Content, contains))
            .Select(post => new
            {
                Post = post,
                Rank = EF.Functions.ILike(post.Content, exact) ? 0 :
                    EF.Functions.ILike(post.Content, prefix) ? 1 : 2
            });
        if (cursor is not null)
        {
            if (cursor.Ticks is null)
            {
                return new([], null);
            }

            var createdAtUtc = new DateTimeOffset(new DateTime(cursor.Ticks.Value, DateTimeKind.Utc));
            ranked = ranked.Where(item =>
                item.Rank > cursor.Rank ||
                item.Rank == cursor.Rank &&
                (item.Post.CreatedAtUtc < createdAtUtc ||
                 item.Post.CreatedAtUtc == createdAtUtc && item.Post.Id.CompareTo(cursor.Id) > 0));
        }

        var rows = await ranked
            .OrderBy(item => item.Rank)
            .ThenByDescending(item => item.Post.CreatedAtUtc)
            .ThenBy(item => item.Post.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        var visible = rows.Take(limit).Select(item => item.Post).ToList();
        var responses = await postsService.LoadResponsesAsync(visible, context.Viewer.UserId, cancellationToken);
        var postContainers = visible.ToDictionary(post => post.Id, post => post.ContainerId);
        return new(
            responses.Select(item => new SearchPostResponse(
                item.Id,
                item.AuthorUserId,
                item.DisplayAuthor,
                Snippet(item.Content),
                item.MediaIds,
                item.CommentCount,
                item.ReactionCounts,
                item.ContainerType ?? "profile",
                postContainers[item.Id],
                item.CreatedAtUtc)).ToList(),
            rows.Count > limit ? EncodeCursor(SearchType.Posts, query, rows[limit - 1].Rank, rows[limit - 1].Post.Id,
                ticks: rows[limit - 1].Post.CreatedAtUtc.UtcDateTime.Ticks) : null);
    }

    private async Task<SearchPage<SearchEventResponse>> SearchEventsAsync(
        string query,
        SearchCursor? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        var contains = "%" + query + "%";
        var events = dbContext.Events.AsNoTracking()
            .Where(item => item.DeletedAtUtc == null && item.Status == EventStatus.Published &&
                item.Privacy == EventPrivacy.Public && item.StartsAtUtc >= DateTimeOffset.UtcNow &&
                (EF.Functions.ILike(item.Name, contains) ||
                 item.Description != null && EF.Functions.ILike(item.Description, contains)));
        if (cursor is not null)
        {
            if (cursor.Ticks is null) return new([], null);
            var cursorTime = new DateTimeOffset(cursor.Ticks.Value, TimeSpan.Zero);
            events = events.Where(item => item.StartsAtUtc > cursorTime ||
                item.StartsAtUtc == cursorTime && item.Id.CompareTo(cursor.Id) > 0);
        }
        var rows = await events
            .OrderBy(item => item.StartsAtUtc).ThenBy(item => item.Id).Take(limit + 1)
            .Select(item => new
            {
                item.Id, item.Name, item.HostType, item.HostId, item.StartsAtUtc, item.LocationType,
                item.LocationName, item.CoverMediaId,
                Going = dbContext.EventParticipants.Count(p => p.EventId == item.Id && p.Status == EventParticipantStatus.Going),
                Interested = dbContext.EventParticipants.Count(p => p.EventId == item.Id && p.Status == EventParticipantStatus.Interested)
            }).ToListAsync(cancellationToken);
        var visible = rows.Take(limit).ToList();
        var groupIds = visible.Where(x => x.HostType == EventHostType.Group).Select(x => x.HostId).ToArray();
        var pageIds = visible.Where(x => x.HostType == EventHostType.Page).Select(x => x.HostId).ToArray();
        var userIds = visible.Where(x => x.HostType == EventHostType.User).Select(x => x.HostId).ToArray();
        var names = await dbContext.Groups.AsNoTracking().Where(x => groupIds.Contains(x.Id)).Select(x => new { x.Id, x.Name })
            .Concat(dbContext.Pages.AsNoTracking().Where(x => pageIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }))
            .Concat(dbContext.UserProfiles.AsNoTracking().Where(x => userIds.Contains(x.UserId)).Select(x => new { Id = x.UserId, Name = x.DisplayName }))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        return new(visible.Select(x => new SearchEventResponse(x.Id, x.Name, x.HostType.ToString().ToLowerInvariant(),
            x.HostId, names.GetValueOrDefault(x.HostId, "Event host"), x.StartsAtUtc,
            x.LocationType.ToString().ToLowerInvariant(), x.LocationName,
            x.CoverMediaId is null ? null : $"/api/events/{x.Id}/cover", x.Going, x.Interested)).ToList(),
            rows.Count > limit ? EncodeCursor(SearchType.Events, query, 0, visible[^1].Id, ticks: visible[^1].StartsAtUtc.UtcTicks) : null);
    }

    private async Task<SearchPage<SearchReelResponse>> SearchReelsAsync(
        SearchContext context,
        string query,
        SearchCursor? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        var reelCandidates = PostVisibility.ApplyDirectAccess(dbContext.Posts.AsNoTracking(), context.Viewer)
            .Where(post => post.PostType == PostType.Reel &&
                           dbContext.PostMedia.Any(postMedia =>
                               postMedia.PostId == post.Id &&
                               dbContext.MediaAssets.Any(asset =>
                                   asset.Id == postMedia.MediaId &&
                                   asset.MediaType == MediaType.Video &&
                                   asset.Status == MediaStatus.Ready &&
                                   asset.DeletedAtUtc == null &&
                                   asset.ProcessedObjectKey != null &&
                                   asset.PosterObjectKey != null &&
                                   asset.DurationMs != null &&
                                   asset.Width != null &&
                                   asset.Height != null)));
        var exact = query;
        var prefix = query + "%";
        var contains = "%" + query + "%";
        var ranked = reelCandidates
            .Where(post => EF.Functions.ILike(post.Content, contains))
            .Select(post => new
            {
                Post = post,
                Rank = EF.Functions.ILike(post.Content, exact) ? 0 :
                    EF.Functions.ILike(post.Content, prefix) ? 1 : 2
            });
        if (cursor is not null)
        {
            if (cursor.Ticks is null)
            {
                return new([], null);
            }

            var createdAtUtc = new DateTimeOffset(new DateTime(cursor.Ticks.Value, DateTimeKind.Utc));
            ranked = ranked.Where(item =>
                item.Rank > cursor.Rank ||
                item.Rank == cursor.Rank &&
                (item.Post.CreatedAtUtc < createdAtUtc ||
                 item.Post.CreatedAtUtc == createdAtUtc && item.Post.Id.CompareTo(cursor.Id) > 0));
        }

        var rows = await ranked
            .OrderBy(item => item.Rank)
            .ThenByDescending(item => item.Post.CreatedAtUtc)
            .ThenBy(item => item.Post.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        var visible = rows.Take(limit).Select(item => item.Post).ToList();
        var responses = await LoadReelResponsesAsync(visible, cancellationToken);
        return new(
            responses,
            rows.Count > limit ? EncodeCursor(SearchType.Reels, query, rows[limit - 1].Rank, rows[limit - 1].Post.Id,
                ticks: rows[limit - 1].Post.CreatedAtUtc.UtcDateTime.Ticks) : null);
    }

    private IQueryable<Post> VisibleStandardPosts(SearchContext context)
    {
        var viewer = context.Viewer;
        var profilePosts = PostVisibility.ApplyDirectAccess(dbContext.Posts.AsNoTracking(), viewer)
            .Where(post => post.PostType == PostType.Standard);
        var groupPosts =
            from post in dbContext.Posts.AsNoTracking()
            join groupItem in dbContext.Groups.AsNoTracking() on post.ContainerId equals groupItem.Id
            where post.PostType == PostType.Standard && post.DeletedAtUtc == null &&
                  post.ContainerType == PostContainerType.Group && groupItem.DeletedAtUtc == null &&
                  (groupItem.Privacy == GroupPrivacy.Public ||
                   dbContext.GroupMembers.Any(member => member.GroupId == groupItem.Id && member.UserId == viewer.UserId)) &&
                  (post.AuthorUserId == viewer.UserId || !viewer.BlockedUserIds.Contains(post.AuthorUserId))
            select post;
        var pagePosts =
            from post in dbContext.Posts.AsNoTracking()
            join page in dbContext.Pages.AsNoTracking() on post.ContainerId equals page.Id
            where post.PostType == PostType.Standard && post.DeletedAtUtc == null &&
                  post.ContainerType == PostContainerType.Page && page.DeletedAtUtc == null &&
                  page.Status == PageStatus.Published
            select post;
        return profilePosts.Concat(groupPosts).Concat(pagePosts);
    }

    private async Task<IReadOnlyList<SearchReelResponse>> LoadReelResponsesAsync(
        IReadOnlyList<Post> reels,
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
            join asset in dbContext.MediaAssets.AsNoTracking() on postMedia.MediaId equals asset.Id
            where reelIds.Contains(postMedia.PostId) && asset.MediaType == MediaType.Video &&
                  asset.Status == MediaStatus.Ready && asset.DeletedAtUtc == null &&
                  asset.ProcessedObjectKey != null && asset.PosterObjectKey != null &&
                  asset.DurationMs != null && asset.Width != null && asset.Height != null
            select new ReelMediaRow(postMedia.PostId, postMedia.MediaId, asset.DurationMs ?? 0,
                asset.Width ?? 0, asset.Height ?? 0))
            .ToDictionaryAsync(row => row.ReelId, cancellationToken);
        var profiles = await dbContext.UserProfiles.AsNoTracking()
            .Where(profile => authorIds.Contains(profile.UserId))
            .Select(profile => new ReelProfileRow(profile.UserId, profile.Username, profile.DisplayName,
                profile.AvatarUrl, profile.AvatarMediaId))
            .ToDictionaryAsync(row => row.UserId, cancellationToken);
        var users = await dbContext.Users.AsNoTracking()
            .Where(user => authorIds.Contains(user.Id))
            .Select(user => new { user.Id, user.UserName })
            .ToDictionaryAsync(row => row.Id, row => row.UserName, cancellationToken);
        var commentCounts = await dbContext.Comments.AsNoTracking()
            .Where(comment => reelIds.Contains(comment.PostId) && comment.DeletedAtUtc == null)
            .GroupBy(comment => comment.PostId)
            .Select(group => new { ReelId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.ReelId, row => row.Count, cancellationToken);
        var reactionCounts = await dbContext.PostReactions.AsNoTracking()
            .Where(reaction => reelIds.Contains(reaction.PostId))
            .GroupBy(reaction => reaction.PostId)
            .Select(group => new { ReelId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.ReelId, row => row.Count, cancellationToken);
        var viewCounts = await dbContext.ReelViews.AsNoTracking()
            .Where(view => reelIds.Contains(view.ReelPostId))
            .GroupBy(view => view.ReelPostId)
            .Select(group => new { ReelId = group.Key, Count = group.LongCount() })
            .ToDictionaryAsync(row => row.ReelId, row => row.Count, cancellationToken);

        return reels.Select(reel =>
        {
            var media = mediaRows[reel.Id];
            var profile = profiles.GetValueOrDefault(reel.AuthorUserId);
            var username = profile?.Username ?? users.GetValueOrDefault(reel.AuthorUserId) ?? reel.AuthorUserId.ToString("N");
            return new SearchReelResponse(
                reel.Id,
                new SearchReelAuthorResponse(
                    reel.AuthorUserId,
                    username,
                    profile?.DisplayName ?? username,
                    profile?.AvatarMediaId is null ? profile?.AvatarUrl : $"/api/users/{reel.AuthorUserId}/avatar"),
                Snippet(reel.Content),
                new SearchReelMediaResponse(
                    media.MediaId,
                    media.DurationMs,
                    media.Width,
                    media.Height,
                    $"/api/reels/{reel.Id}/video/access",
                    $"/api/reels/{reel.Id}/poster/access"),
                commentCounts.GetValueOrDefault(reel.Id),
                reactionCounts.GetValueOrDefault(reel.Id),
                viewCounts.GetValueOrDefault(reel.Id),
                reel.CreatedAtUtc);
        }).ToList();
    }

    private async Task<SearchContext> CreateContextAsync(Guid viewerUserId, CancellationToken cancellationToken)
    {
        var relationships = await friendsService.GetAccessSnapshotAsync(viewerUserId, cancellationToken);
        return new(new PostViewerContext(
            viewerUserId,
            relationships.FriendUserIds,
            relationships.BlockedUserIds));
    }

    private static ApplicationResult<GlobalSearchResponse> ToGlobal(SearchPage<SearchPersonResponse> page) =>
        ApplicationResult<GlobalSearchResponse>.Success(new(page.Items, [], [], [], [], page.NextCursor));

    private static ApplicationResult<GlobalSearchResponse> ToGlobal(SearchPage<SearchGroupResponse> page) =>
        ApplicationResult<GlobalSearchResponse>.Success(new([], page.Items, [], [], [], page.NextCursor));

    private static ApplicationResult<GlobalSearchResponse> ToGlobal(SearchPage<SearchPageResponse> page) =>
        ApplicationResult<GlobalSearchResponse>.Success(new([], [], page.Items, [], [], page.NextCursor));

    private static ApplicationResult<GlobalSearchResponse> ToGlobal(SearchPage<SearchPostResponse> page) =>
        ApplicationResult<GlobalSearchResponse>.Success(new([], [], [], page.Items, [], page.NextCursor));

    private static ApplicationResult<GlobalSearchResponse> ToGlobal(SearchPage<SearchReelResponse> page) =>
        ApplicationResult<GlobalSearchResponse>.Success(new([], [], [], [], page.Items, page.NextCursor));

    private static ApplicationResult<GlobalSearchResponse> ToGlobal(SearchPage<SearchEventResponse> page) =>
        ApplicationResult<GlobalSearchResponse>.Success(new([], [], [], [], [], page.NextCursor, null, page.Items));

    private static bool TryNormalizeQuery(string? queryValue, out string query, out ApplicationError? error)
    {
        query = queryValue?.Trim() ?? string.Empty;
        if (query.Length < MinimumQueryLength || query.Length > MaximumQueryLength)
        {
            error = new ApplicationError(
                "invalid_search_query",
                $"Query must contain between {MinimumQueryLength} and {MaximumQueryLength} characters after trimming.",
                ApplicationErrorType.Validation);
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryParseType(string? value, out SearchType type)
    {
        type = value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "all" => SearchType.All,
            "people" => SearchType.People,
            "groups" => SearchType.Groups,
            "pages" => SearchType.Pages,
            "posts" => SearchType.Posts,
            "reels" => SearchType.Reels,
            "events" => SearchType.Events,
            _ => SearchType.Invalid
        };
        return type != SearchType.Invalid;
    }

    private static bool TryDecodeCursor(string? value, SearchType type, string query, out SearchCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        try
        {
            var encoded = value.Replace('-', '+').Replace('_', '/');
            encoded = encoded.PadRight(encoded.Length + (4 - encoded.Length % 4) % 4, '=');
            var payload = JsonSerializer.Deserialize<SearchCursor>(Convert.FromBase64String(encoded));
            if (payload is null ||
                payload.Version != CursorVersion ||
                payload.Type != TypeName(type) ||
                payload.QueryHash != QueryHash(type, query) ||
                payload.Id == Guid.Empty ||
                payload.Rank < 0)
            {
                return false;
            }

            cursor = payload;
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException)
        {
            return false;
        }
    }

    private static string EncodeCursor(
        SearchType type,
        string query,
        int rank,
        Guid id,
        string? name = null,
        int? count = null,
        long? ticks = null)
    {
        var payload = new SearchCursor(CursorVersion, TypeName(type), QueryHash(type, query), rank, id, name, count, ticks);
        return Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payload))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string QueryHash(SearchType type, string query) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(TypeName(type) + ":" + query)));

    private static string TypeName(SearchType type) => type.ToString().ToLowerInvariant();

    private static string Snippet(string value) => value.Length <= 280 ? value : value[..280].TrimEnd() + "…";

    private static string? ShortBio(string? value) => value is null ? null : Snippet(value);

    private static ApplicationResult<T> Validation<T>(string code, string message) =>
        ApplicationResult<T>.Failure(new ApplicationError(code, message, ApplicationErrorType.Validation));

    private static ApplicationResult<T> Failure<T>(ApplicationError error) => ApplicationResult<T>.Failure(error);

    private enum SearchType
    {
        Invalid,
        All,
        People,
        Groups,
        Pages,
        Posts,
        Reels,
        Events
    }

    private sealed record SearchContext(PostViewerContext Viewer);

    private sealed record SearchPage<T>(IReadOnlyList<T> Items, string? NextCursor);

    private sealed record SearchCursor(
        string Version,
        string Type,
        string QueryHash,
        int Rank,
        Guid Id,
        string? Name,
        int? Count,
        long? Ticks);

    private sealed record PersonRow(
        Guid UserId,
        string Username,
        string DisplayName,
        string? AvatarUrl,
        Guid? AvatarMediaId,
        string? Bio,
        int FollowerCount,
        int FollowingCount,
        bool IsFollowing,
        bool IsFollowedBy,
        string FriendshipState,
        int Rank);

    private sealed record GroupRow(
        Guid GroupId,
        string Name,
        string? Description,
        GroupPrivacy Privacy,
        Guid? CoverMediaId,
        int MemberCount,
        bool IsMember,
        bool HasPendingRequest,
        int Rank);

    private sealed record PageRow(
        Guid PageId,
        string Name,
        string Username,
        string Category,
        string? Bio,
        Guid? AvatarMediaId,
        int FollowerCount,
        bool ViewerIsFollowing,
        int Rank);

    private sealed record ReelMediaRow(Guid ReelId, Guid MediaId, long DurationMs, int Width, int Height);

    private sealed record ReelProfileRow(
        Guid UserId,
        string Username,
        string DisplayName,
        string? AvatarUrl,
        Guid? AvatarMediaId);
}
