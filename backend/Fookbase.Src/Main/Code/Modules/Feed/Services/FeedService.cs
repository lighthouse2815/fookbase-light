using System.Globalization;
using System.Text;
using Fookbase.Api.Modules.Feed.DTOs.Responses;
using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Feed.Services;

public sealed class FeedService(
    FookbaseDbContext dbContext,
    FriendsService friendsService)
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 50;

    public async Task<FeedPageResponse> GetHomeFeedAsync(
        Guid viewerUserId,
        string? cursorValue,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var cursor = string.IsNullOrWhiteSpace(cursorValue)
            ? null
            : DecodeCursor(cursorValue);
        var relationships = await friendsService.GetAccessSnapshotAsync(
            viewerUserId,
            cancellationToken);
        var viewer = new PostViewerContext(
            viewerUserId,
            relationships.FriendUserIds,
            relationships.BlockedUserIds);
        var query = PostVisibility.ApplyHomeFeed(dbContext.Posts.AsNoTracking(), viewer)
            .Where(post => post.PostType == PostType.Standard);
        if (cursor is not null)
        {
            query = query.Where(post =>
                post.CreatedAtUtc < cursor.CreatedAtUtc ||
                (post.CreatedAtUtc == cursor.CreatedAtUtc &&
                 post.Id.CompareTo(cursor.Id) < 0));
        }

        var candidates = await query
            .OrderByDescending(post => post.CreatedAtUtc)
            .ThenByDescending(post => post.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        var posts = candidates.Take(limit).ToList();
        var hasMore = candidates.Count > limit;
        return new FeedPageResponse(
            await BuildItemsAsync(posts, viewerUserId, cancellationToken),
            hasMore ? EncodeCursor(posts[^1]) : null);
    }

    public static bool IsValidCursor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        try
        {
            _ = DecodeCursor(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private async Task<IReadOnlyList<FeedItemResponse>> BuildItemsAsync(
        IReadOnlyList<Post> posts,
        Guid viewerUserId,
        CancellationToken cancellationToken)
    {
        if (posts.Count == 0)
        {
            return [];
        }

        var postIds = posts.Select(post => post.Id).ToArray();
        var authorUserIds = posts.Select(post => post.AuthorUserId).Distinct().ToArray();
        var profiles = await dbContext.UserProfiles.AsNoTracking()
            .Where(profile => authorUserIds.Contains(profile.UserId))
            .Select(profile => new AuthorProfile(
                profile.UserId,
                profile.Username,
                profile.DisplayName,
                profile.AvatarUrl,
                profile.AvatarMediaId))
            .ToDictionaryAsync(profile => profile.UserId, cancellationToken);
        var users = await dbContext.Users.AsNoTracking()
            .Where(user => authorUserIds.Contains(user.Id))
            .Select(user => new AuthorUser(user.Id, user.UserName))
            .ToDictionaryAsync(user => user.UserId, cancellationToken);
        var media = await (
            from postMedia in dbContext.PostMedia.AsNoTracking()
            join asset in dbContext.MediaAssets.AsNoTracking() on postMedia.MediaId equals asset.Id
            where postIds.Contains(postMedia.PostId) &&
                  asset.Status == MediaStatus.Ready &&
                  asset.DeletedAtUtc == null
            orderby postMedia.PostId, postMedia.SortOrder
            select new FeedMediaRow(
                postMedia.PostId,
                asset.Id,
                asset.MediaType,
                asset.ContentType))
            .ToListAsync(cancellationToken);
        var commentCounts = await dbContext.Comments.AsNoTracking()
            .Where(comment => postIds.Contains(comment.PostId) && comment.DeletedAtUtc == null)
            .GroupBy(comment => comment.PostId)
            .Select(group => new { PostId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.PostId, item => item.Count, cancellationToken);
        var reactionRows = await dbContext.PostReactions.AsNoTracking()
            .Where(reaction => postIds.Contains(reaction.PostId))
            .GroupBy(reaction => new { reaction.PostId, reaction.Type })
            .Select(group => new ReactionCountRow(group.Key.PostId, group.Key.Type, group.Count()))
            .ToListAsync(cancellationToken);
        var viewerReactions = await dbContext.PostReactions.AsNoTracking()
            .Where(reaction =>
                postIds.Contains(reaction.PostId) &&
                reaction.UserId == viewerUserId)
            .ToDictionaryAsync(
                reaction => reaction.PostId,
                reaction => reaction.Type.ToString().ToLowerInvariant(),
                cancellationToken);
        var mediaByPost = media
            .GroupBy(item => item.PostId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<FeedMediaRow>)group.ToList());
        var reactionsByPost = reactionRows
            .GroupBy(item => item.PostId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<string, int>)group.ToDictionary(
                    item => item.Type.ToString().ToLowerInvariant(),
                    item => item.Count));

        return posts.Select(post =>
        {
            var profile = profiles.GetValueOrDefault(post.AuthorUserId);
            var user = users.GetValueOrDefault(post.AuthorUserId);
            var username = profile?.Username ?? user?.Username ?? post.AuthorUserId.ToString("N");
            var displayName = profile?.DisplayName ?? username;
            var avatarUrl = profile?.AvatarMediaId is not null
                ? "/api/users/" + post.AuthorUserId + "/avatar"
                : profile?.AvatarUrl;
            var postMedia = mediaByPost.GetValueOrDefault(post.Id) ?? [];
            var reactionCounts = reactionsByPost.GetValueOrDefault(post.Id) ??
                new Dictionary<string, int>();

            return new FeedItemResponse(
                post.Id,
                post.Content,
                PrivacyName(post.Privacy),
                post.CreatedAtUtc,
                post.UpdatedAtUtc,
                new FeedAuthorResponse(post.AuthorUserId, username, displayName, avatarUrl),
                postMedia.Select(item => item.MediaId).ToList(),
                postMedia.Select(item => new FeedMediaResponse(
                    item.MediaId,
                    item.MediaType.ToString().ToLowerInvariant(),
                    item.ContentType)).ToList(),
                commentCounts.GetValueOrDefault(post.Id),
                reactionCounts.Values.Sum(),
                reactionCounts,
                viewerReactions.GetValueOrDefault(post.Id));
        }).ToList();
    }

    private static string PrivacyName(PostPrivacy privacy) => privacy switch
    {
        PostPrivacy.Public => "public",
        PostPrivacy.Friends => "friends",
        PostPrivacy.OnlyMe => "onlyMe",
        _ => throw new ArgumentOutOfRangeException(nameof(privacy), privacy, null)
    };

    private static string EncodeCursor(Post post)
    {
        var payload = post.CreatedAtUtc.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture) +
            ":" + post.Id.ToString("N");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static FeedCursor DecodeCursor(string value)
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
                throw new FormatException("The feed cursor is invalid.");
            }

            return new FeedCursor(
                new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc)),
                id);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException("The feed cursor is invalid.", exception);
        }
    }

    private sealed record FeedCursor(DateTimeOffset CreatedAtUtc, Guid Id);

    private sealed record AuthorProfile(
        Guid UserId,
        string Username,
        string DisplayName,
        string? AvatarUrl,
        Guid? AvatarMediaId);

    private sealed record AuthorUser(Guid UserId, string? Username);

    private sealed record FeedMediaRow(
        Guid PostId,
        Guid MediaId,
        MediaType MediaType,
        string ContentType);

    private sealed record ReactionCountRow(Guid PostId, ReactionType Type, int Count);
}
