using Fookbase.Api.Modules.Posts.Entities;

namespace Fookbase.Api.Modules.Posts.Services;

public static class PostVisibility
{
    public static bool CanDirectlyAccess(Post post, PostViewerContext? viewer)
    {
        if (post.DeletedAtUtc is not null || post.ContainerType != PostContainerType.Profile)
        {
            return false;
        }

        if (viewer is null)
        {
            return post.Privacy == PostPrivacy.Public;
        }

        return post.AuthorUserId == viewer.UserId ||
            (!viewer.BlockedUserIds.Contains(post.AuthorUserId) &&
             (post.Privacy == PostPrivacy.Public ||
              (post.Privacy == PostPrivacy.Friends &&
               viewer.FriendUserIds.Contains(post.AuthorUserId))));
    }

    public static IQueryable<Post> ApplyDirectAccess(
        IQueryable<Post> posts,
        PostViewerContext? viewer)
    {
        var activePosts = posts.Where(post =>
            post.DeletedAtUtc == null && post.ContainerType == PostContainerType.Profile);
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
            post.ContainerType == PostContainerType.Profile &&
            (post.AuthorUserId == viewerUserId ||
             (friendUserIds.Contains(post.AuthorUserId) &&
              !blockedUserIds.Contains(post.AuthorUserId) &&
              (post.Privacy == PostPrivacy.Public ||
               post.Privacy == PostPrivacy.Friends))));
    }
}
