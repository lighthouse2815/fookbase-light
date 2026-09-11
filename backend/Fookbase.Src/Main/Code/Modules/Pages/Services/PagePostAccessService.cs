using Fookbase.Api.Modules.Pages.Entities;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Pages.Services;

public sealed class PagePostAccessService(FookbaseDbContext dbContext)
{
    public async Task<bool> CanViewPageAsync(Guid pageId, Guid? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var page = await dbContext.Pages.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == pageId && item.DeletedAtUtc == null,
            cancellationToken);
        if (page is null)
        {
            return false;
        }

        return page.Status == PageStatus.Published ||
            viewerUserId is not null && await IsMemberAsync(pageId, viewerUserId.Value, cancellationToken);
    }

    public Task<bool> IsMemberAsync(Guid pageId, Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.PageMembers.AsNoTracking().AnyAsync(
            member => member.PageId == pageId && member.UserId == userId, cancellationToken);

    public Task<PageRole?> GetRoleAsync(Guid pageId, Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.PageMembers.AsNoTracking()
            .Where(member => member.PageId == pageId && member.UserId == userId)
            .Select(member => (PageRole?)member.Role)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> CanCreatePostAsync(Guid pageId, Guid userId, CancellationToken cancellationToken = default) =>
        await IsActivePageAsync(pageId, cancellationToken) && await dbContext.PageMembers.AsNoTracking().AnyAsync(member =>
            member.PageId == pageId && member.UserId == userId &&
            (member.Role == PageRole.Owner || member.Role == PageRole.Admin || member.Role == PageRole.Editor),
            cancellationToken);

    public async Task<bool> CanModeratePostAsync(Guid pageId, Guid userId, CancellationToken cancellationToken = default) =>
        await IsActivePageAsync(pageId, cancellationToken) && await dbContext.PageMembers.AsNoTracking().AnyAsync(member =>
            member.PageId == pageId && member.UserId == userId &&
            (member.Role == PageRole.Owner || member.Role == PageRole.Admin || member.Role == PageRole.Moderator),
            cancellationToken);

    public async Task<bool> CanAccessPostAsync(Post post, PostViewerContext? viewer,
        CancellationToken cancellationToken = default) =>
        post.ContainerType == PostContainerType.Page &&
        await CanViewPageAsync(post.ContainerId, viewer?.UserId, cancellationToken);

    public async Task<bool> CanParticipateAsync(Post post, PostViewerContext actor,
        CancellationToken cancellationToken = default) =>
        post.ContainerType == PostContainerType.Page &&
        await CanViewPageAsync(post.ContainerId, actor.UserId, cancellationToken);

    public async Task<bool> CanModerateCommentAsync(Guid postId, Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var pageId = await dbContext.Posts.AsNoTracking()
            .Where(post => post.Id == postId && post.DeletedAtUtc == null && post.ContainerType == PostContainerType.Page)
            .Select(post => (Guid?)post.ContainerId)
            .SingleOrDefaultAsync(cancellationToken);
        return pageId is not null && await CanModeratePostAsync(pageId.Value, actorUserId, cancellationToken);
    }

    private Task<bool> IsActivePageAsync(Guid pageId, CancellationToken cancellationToken) =>
        dbContext.Pages.AsNoTracking().AnyAsync(
            page => page.Id == pageId && page.DeletedAtUtc == null, cancellationToken);
}
