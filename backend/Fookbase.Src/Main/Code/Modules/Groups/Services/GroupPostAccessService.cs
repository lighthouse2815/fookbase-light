using Fookbase.Api.Modules.Groups.Entities;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Persistence;
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

    public async Task<bool> CanAccessPostAsync(
        Post post,
        PostViewerContext? viewer,
        CancellationToken cancellationToken = default)
    {
        if (post.ContainerType != PostContainerType.Group)
        {
            return false;
        }

        if (!await CanViewGroupAsync(post.ContainerId, viewer?.UserId, cancellationToken))
        {
            return false;
        }

        return viewer is null ||
            viewer.UserId == post.AuthorUserId ||
            !viewer.BlockedUserIds.Contains(post.AuthorUserId);
    }

    public async Task<bool> CanParticipateAsync(
        Post post,
        PostViewerContext actor,
        CancellationToken cancellationToken = default) =>
        post.ContainerType == PostContainerType.Group &&
        !actor.BlockedUserIds.Contains(post.AuthorUserId) &&
        await IsActiveMemberAsync(post.ContainerId, actor.UserId, cancellationToken);
}
