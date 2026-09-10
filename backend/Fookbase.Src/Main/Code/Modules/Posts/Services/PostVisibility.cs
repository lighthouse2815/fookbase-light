using Fookbase.Api.Modules.Posts.Entities;

namespace Fookbase.Api.Modules.Posts.Services;

public static class PostVisibility
{
    public static IQueryable<Post> ApplyDirectAccess(
        IQueryable<Post> posts,
        PostViewerContext? viewer)
    {
        var activePosts = posts.Where(post => post.DeletedAtUtc == null);
        if (viewer is null)
        {
            return activePosts.Where(post => post.Privacy == PostPrivacy.Public);
        }

        var viewerUserId = viewer.UserId;
        var friendUserIds = viewer.FriendUserIds;
        var blockedUserIds = viewer.BlockedUserIds;
        return activePosts.Where(post =>
            post.AuthorUserId == viewerUserId ||
            (!blockedUserIds.Contains(post.AuthorUserId) &&
             (post.Privacy == PostPrivacy.Public ||
              (post.Privacy == PostPrivacy.Friends && friendUserIds.Contains(post.AuthorUserId)))));
    }

    public static IQueryable<Post> ApplyHomeFeed(
        IQueryable<Post> posts,
        PostViewerContext viewer)
    {
        var viewerUserId = viewer.UserId;
        var friendUserIds = viewer.FriendUserIds;
        var blockedUserIds = viewer.BlockedUserIds;
        return posts.Where(post =>
            post.DeletedAtUtc == null &&
            (post.AuthorUserId == viewerUserId ||
             (friendUserIds.Contains(post.AuthorUserId) &&
              !blockedUserIds.Contains(post.AuthorUserId) &&
              (post.Privacy == PostPrivacy.Public ||
               post.Privacy == PostPrivacy.Friends))));
    }
}
