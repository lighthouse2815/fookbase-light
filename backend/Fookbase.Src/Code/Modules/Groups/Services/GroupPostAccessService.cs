using Fookbase.Api.Modules.Groups.Entities;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Posts.Services;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Groups.Services;

public sealed class GroupPostAccessService(FookbaseDbContext dbContext)
{
    public async Task<bool> CanViewGroupAsync(
        Guid groupId,
        Guid? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var group = await dbContext.Groups.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == groupId && item.DeletedAtUtc == null,
            cancellationToken);
        return group is not null &&
            await CanViewGroupAsync(group, viewerUserId, cancellationToken);
    }

    public async Task<bool> CanViewGroupAsync(
        Group group,
        Guid? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        if (group.DeletedAtUtc is not null)
        {
            return false;
        }

        if (group.Privacy == GroupPrivacy.Public)
        {
            return true;
        }

        return viewerUserId is not null &&
            await IsActiveMemberAsync(group.Id, viewerUserId.Value, cancellationToken);
    }

    public Task<bool> IsActiveMemberAsync(
        Guid groupId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        dbContext.GroupMembers.AsNoTracking().AnyAsync(
            member => member.GroupId == groupId && member.UserId == userId,
            cancellationToken);

    public async Task<bool> CanCreatePostAsync(
        Guid groupId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Groups.AsNoTracking().AnyAsync(
            group => group.Id == groupId && group.DeletedAtUtc == null,
            cancellationToken) &&
        await IsActiveMemberAsync(groupId, userId, cancellationToken);

    public Task<bool> CanAccessPostAsync(
        Post post,
        PostViewerContext? viewer,
        CancellationToken cancellationToken = default) =>
        ApplyDirectAccess(dbContext.Posts.AsNoTracking(), viewer)
            .AnyAsync(item => item.Id == post.Id, cancellationToken);

    public IQueryable<Post> ApplyDirectAccess(
        IQueryable<Post> posts,
        PostViewerContext? viewer)
    {
        var activePosts = posts.Where(post =>
            post.DeletedAtUtc == null && post.ContainerType == PostContainerType.Group);
        if (viewer is null)
        {
            return activePosts.Where(post => dbContext.Groups.Any(group =>
                group.Id == post.ContainerId && group.DeletedAtUtc == null &&
                group.Privacy == GroupPrivacy.Public));
        }

        var viewerUserId = viewer.UserId;
        var blockedUserIds = viewer.BlockedUserIds;
        return activePosts.Where(post =>
            (post.AuthorUserId == viewerUserId || !blockedUserIds.Contains(post.AuthorUserId)) &&
            dbContext.Groups.Any(group =>
                group.Id == post.ContainerId && group.DeletedAtUtc == null &&
                (group.Privacy == GroupPrivacy.Public || dbContext.GroupMembers.Any(member =>
                    member.GroupId == group.Id && member.UserId == viewerUserId))));
    }

    public async Task<bool> CanParticipateAsync(
        Post post,
        PostViewerContext actor,
        CancellationToken cancellationToken = default) =>
        post.ContainerType == PostContainerType.Group &&
        !actor.BlockedUserIds.Contains(post.AuthorUserId) &&
        await IsActiveMemberAsync(post.ContainerId, actor.UserId, cancellationToken);
}
