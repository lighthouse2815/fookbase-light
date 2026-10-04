using Fookbase.Api.Modules.Pages.Entities;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Posts.Services;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Pages.Services;

public sealed class PagePostAccessService(FookbaseDbContext dbContext)
{
    public Task<bool> CanViewPageAsync(Guid pageId, Guid? viewerUserId,
        CancellationToken cancellationToken = default) =>
        VisiblePages(viewerUserId).AnyAsync(page => page.Id == pageId, cancellationToken);

    public IQueryable<Post> ApplyPublishedAccess(IQueryable<Post> posts)
    {
        var publishedPages = VisiblePages(null);
        return posts.Where(post =>
            post.DeletedAtUtc == null && post.ContainerType == PostContainerType.PAGE &&
            publishedPages.Any(page => page.Id == post.ContainerId));
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
            (member.Role == PageRole.OWNER || member.Role == PageRole.ADMIN || member.Role == PageRole.EDITOR),
            cancellationToken);

    public async Task<bool> CanModeratePostAsync(Guid pageId, Guid userId, CancellationToken cancellationToken = default) =>
        await IsActivePageAsync(pageId, cancellationToken) && await dbContext.PageMembers.AsNoTracking().AnyAsync(member =>
            member.PageId == pageId && member.UserId == userId &&
            (member.Role == PageRole.OWNER || member.Role == PageRole.ADMIN || member.Role == PageRole.MODERATOR),
            cancellationToken);

    public async Task<bool> CanAccessPostAsync(Post post, PostViewerContext? viewer,
        CancellationToken cancellationToken = default) =>
        post.ContainerType == PostContainerType.PAGE &&
        await CanViewPageAsync(post.ContainerId, viewer?.UserId, cancellationToken);

    public async Task<bool> CanParticipateAsync(Post post, PostViewerContext actor,
        CancellationToken cancellationToken = default) =>
        post.ContainerType == PostContainerType.PAGE &&
        await CanViewPageAsync(post.ContainerId, actor.UserId, cancellationToken);

    public async Task<bool> CanModerateCommentAsync(Guid postId, Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var pageId = await dbContext.Posts.AsNoTracking()
            .Where(post => post.Id == postId && post.DeletedAtUtc == null && post.ContainerType == PostContainerType.PAGE)
            .Select(post => (Guid?)post.ContainerId)
            .SingleOrDefaultAsync(cancellationToken);
        return pageId is not null && await CanModeratePostAsync(pageId.Value, actorUserId, cancellationToken);
    }

    private Task<bool> IsActivePageAsync(Guid pageId, CancellationToken cancellationToken) =>
        dbContext.Pages.AsNoTracking().AnyAsync(
            page => page.Id == pageId && page.DeletedAtUtc == null, cancellationToken);

    private IQueryable<Page> VisiblePages(Guid? viewerUserId)
    {
        var pages = dbContext.Pages.AsNoTracking().Where(page => page.DeletedAtUtc == null);
        return viewerUserId is null
            ? pages.Where(page => page.Status == PageStatus.PUBLISHED)
            : pages.Where(page => page.Status == PageStatus.PUBLISHED ||
                dbContext.PageMembers.Any(member =>
                    member.PageId == page.Id && member.UserId == viewerUserId.Value));
    }
}
