using Fookbase.Api.Modules.Notifications.Domain.Enums;
using System.Text;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Notifications.Entities;
using Fookbase.Api.Modules.Notifications.Services;
using Fookbase.Api.Modules.Pages.DTOs.Requests;
using Fookbase.Api.Modules.Pages.DTOs.Responses;
using Fookbase.Api.Modules.Pages.Domain.Enums;
using Fookbase.Api.Modules.Pages.Entities;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Posts.DTOs.Requests;
using Fookbase.Api.Modules.Posts.DTOs.Responses;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Shared.ErrorHandling;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Pages.Services;

public sealed class PagesService(
    FookbaseDbContext dbContext,
    PagePostAccessService pagePostAccessService,
    MediaService mediaService,
    NotificationService notificationService,
    PostsUseCase postsUseCase,
    PostsService postsService,
    TimeProvider timeProvider)
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 50;

    private static readonly ISet<string> ReservedUsernames = new HashSet<string>(StringComparer.Ordinal)
    {
        "admin", "api", "auth", "feed", "groups", "health", "media", "messages", "pages", "posts",
        "reels", "settings", "stories", "support", "system", "users"
    };

    public async Task<ApplicationResult<PageResponse>> CreateAsync(Guid actorUserId, CreatePageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidatePage(request.Name, request.Username, request.Category, request.Bio, out var username, out var error))
        {
            return ApplicationResult<PageResponse>.Failure(error!);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow();
            var page = Page.Create(Guid.NewGuid(), request.Name, username, request.Category, request.Bio, actorUserId, now);
            dbContext.Pages.Add(page);
            dbContext.PageMembers.Add(PageMember.Create(page.Id, actorUserId, PageRole.OWNER, now));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ApplicationResult<PageResponse>.Success(await ToResponseAsync(page, actorUserId, PageRole.OWNER, cancellationToken));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Conflict<PageResponse>("page_username_taken", "This Page username is already in use.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<ApplicationResult<PageResponse>> GetAsync(string idOrUsername, Guid? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var page = await FindPageAsync(idOrUsername, cancellationToken);
        if (page is null)
        {
            return NotFound<PageResponse>();
        }

        var role = viewerUserId is null ? null : await pagePostAccessService.GetRoleAsync(page.Id, viewerUserId.Value, cancellationToken);
        if (page.Status != PageStatus.PUBLISHED && role is null)
        {
            return NotFound<PageResponse>();
        }

        return ApplicationResult<PageResponse>.Success(await ToResponseAsync(page, viewerUserId, role, cancellationToken));
    }

    public async Task<ApplicationResult<PageResponse>> UpdateAsync(Guid actorUserId, Guid pageId, UpdatePageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidatePage(request.Name, request.Username, request.Category, request.Bio, out var username, out var error))
        {
            return ApplicationResult<PageResponse>.Failure(error!);
        }

        var page = await dbContext.Pages.SingleOrDefaultAsync(item => item.Id == pageId && item.DeletedAtUtc == null, cancellationToken);
        if (page is null)
        {
            return NotFound<PageResponse>();
        }

        var role = await pagePostAccessService.GetRoleAsync(pageId, actorUserId, cancellationToken);
        if (role is not (PageRole.OWNER or PageRole.ADMIN))
        {
            return Forbidden<PageResponse>();
        }

        try
        {
            page.Update(request.Name, username, request.Category, request.Bio, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            return ApplicationResult<PageResponse>.Success(await ToResponseAsync(page, actorUserId, role, cancellationToken));
        }
        catch (DbUpdateException)
        {
            return Conflict<PageResponse>("page_username_taken", "This Page username is already in use.");
        }
    }

    public async Task<ApplicationResult<PageResponse>> SetMediaAsync(Guid actorUserId, Guid pageId, SetPageMediaRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = await dbContext.Pages.SingleOrDefaultAsync(item => item.Id == pageId && item.DeletedAtUtc == null, cancellationToken);
        if (page is null)
        {
            return NotFound<PageResponse>();
        }

        var role = await pagePostAccessService.GetRoleAsync(pageId, actorUserId, cancellationToken);
        if (role is not (PageRole.OWNER or PageRole.ADMIN))
        {
            return Forbidden<PageResponse>();
        }

        var avatarMediaId = request.RemoveAvatar ? null : request.AvatarMediaId ?? page.AvatarMediaId;
        var coverMediaId = request.RemoveCover ? null : request.CoverMediaId ?? page.CoverMediaId;
        var changedMediaIds = new[]
        {
            request.RemoveAvatar ? null : request.AvatarMediaId,
            request.RemoveCover ? null : request.CoverMediaId
        }.OfType<Guid>().Distinct();
        foreach (var mediaId in changedMediaIds)
        {
            var validation = await mediaService.ValidatePageImageAsync(actorUserId, mediaId, cancellationToken);
            if (!validation.Succeeded)
            {
                return ApplicationResult<PageResponse>.Failure(validation.Error!);
            }
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            page.SetMedia(avatarMediaId, coverMediaId, timeProvider.GetUtcNow());
            await mediaService.SynchronizePageReferencesAsync(pageId, avatarMediaId, coverMediaId, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ApplicationResult<PageResponse>.Success(await ToResponseAsync(page, actorUserId, role, cancellationToken));
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<ApplicationResult> DeleteAsync(Guid actorUserId, Guid pageId, CancellationToken cancellationToken = default)
    {
        var page = await dbContext.Pages.SingleOrDefaultAsync(item => item.Id == pageId && item.DeletedAtUtc == null, cancellationToken);
        if (page is null)
        {
            return NotFound();
        }

        if (await pagePostAccessService.GetRoleAsync(pageId, actorUserId, cancellationToken) != PageRole.OWNER)
        {
            return Forbidden();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            page.Delete(timeProvider.GetUtcNow());
            await mediaService.SynchronizePageReferencesAsync(pageId, null, null, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ApplicationResult.Success();
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<ApplicationResult<PageResponse>> PublishAsync(Guid actorUserId, Guid pageId,
        CancellationToken cancellationToken = default) =>
        await ChangeStatusAsync(actorUserId, pageId, true, cancellationToken);

    public async Task<ApplicationResult<PageResponse>> UnpublishAsync(Guid actorUserId, Guid pageId,
        CancellationToken cancellationToken = default) =>
        await ChangeStatusAsync(actorUserId, pageId, false, cancellationToken);

    public async Task<ApplicationResult> FollowAsync(Guid actorUserId, Guid pageId, CancellationToken cancellationToken = default)
    {
        var published = await dbContext.Pages.AsNoTracking().AnyAsync(
            page => page.Id == pageId && page.DeletedAtUtc == null && page.Status == PageStatus.PUBLISHED, cancellationToken);
        if (!published)
        {
            return NotFound();
        }

        if (!await dbContext.PageFollowers.AnyAsync(follower => follower.PageId == pageId && follower.UserId == actorUserId, cancellationToken))
        {
            dbContext.PageFollowers.Add(PageFollower.Create(pageId, actorUserId, timeProvider.GetUtcNow()));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult> UnfollowAsync(Guid actorUserId, Guid pageId, CancellationToken cancellationToken = default)
    {
        await dbContext.PageFollowers
            .Where(follower => follower.PageId == pageId && follower.UserId == actorUserId)
            .ExecuteDeleteAsync(cancellationToken);
        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult<PageCursorPageResponse<PageResponse>>> GetMineAsync(Guid actorUserId, string? cursor,
        int limit, CancellationToken cancellationToken = default)
    {
        if (!TryPagination(limit, out var error) || !TryDecodeTimelineCursor(cursor, out var createdAtUtc, out var id))
        {
            return ApplicationResult<PageCursorPageResponse<PageResponse>>.Failure(error ?? InvalidCursor());
        }

        var query = from page in dbContext.Pages.AsNoTracking()
                    join member in dbContext.PageMembers.AsNoTracking() on page.Id equals member.PageId
                    where member.UserId == actorUserId && page.DeletedAtUtc == null
                    select page;
        if (createdAtUtc is not null)
        {
            query = query.Where(page => page.CreatedAtUtc < createdAtUtc ||
                page.CreatedAtUtc == createdAtUtc && page.Id.CompareTo(id!.Value) < 0);
        }

        var pages = await query.OrderByDescending(page => page.CreatedAtUtc).ThenByDescending(page => page.Id)
            .Take(limit + 1).ToListAsync(cancellationToken);
        return ApplicationResult<PageCursorPageResponse<PageResponse>>.Success(
            await ToCursorPageAsync(pages, actorUserId, limit, cancellationToken));
    }

    public async Task<ApplicationResult<PageCursorPageResponse<PageResponse>>> GetFollowingAsync(Guid actorUserId, string? cursor,
        int limit, CancellationToken cancellationToken = default)
    {
        if (!TryPagination(limit, out var error) || !TryDecodeTimelineCursor(cursor, out var followedAtUtc, out var id))
        {
            return ApplicationResult<PageCursorPageResponse<PageResponse>>.Failure(error ?? InvalidCursor());
        }

        var query = from page in dbContext.Pages.AsNoTracking()
                    join follower in dbContext.PageFollowers.AsNoTracking() on page.Id equals follower.PageId
                    where follower.UserId == actorUserId && page.DeletedAtUtc == null && page.Status == PageStatus.PUBLISHED
                    select new { Page = page, follower.FollowedAtUtc };
        if (followedAtUtc is not null)
        {
            query = query.Where(item => item.FollowedAtUtc < followedAtUtc ||
                item.FollowedAtUtc == followedAtUtc && item.Page.Id.CompareTo(id!.Value) < 0);
        }

        var results = await query.OrderByDescending(item => item.FollowedAtUtc).ThenByDescending(item => item.Page.Id)
            .Take(limit + 1).ToListAsync(cancellationToken);
        var pages = results.Select(item => item.Page).ToList();
        return ApplicationResult<PageCursorPageResponse<PageResponse>>.Success(
            await ToCursorPageAsync(pages, actorUserId, limit, cancellationToken, results.Select(item => item.FollowedAtUtc).ToList()));
    }

    public async Task<ApplicationResult<PageCursorPageResponse<PageResponse>>> DiscoverAsync(string? queryText, string? cursor,
        int limit, Guid? viewerUserId, CancellationToken cancellationToken = default)
    {
        if (!TryPagination(limit, out var error) || !TryDecodeNameCursor(cursor, out var name, out var id))
        {
            return ApplicationResult<PageCursorPageResponse<PageResponse>>.Failure(error ?? InvalidCursor());
        }

        var normalized = queryText?.Trim();
        var query = dbContext.Pages.AsNoTracking().Where(page => page.DeletedAtUtc == null && page.Status == PageStatus.PUBLISHED);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            var pattern = $"%{normalized}%";
            query = query.Where(page => EF.Functions.ILike(page.Name, pattern) ||
                EF.Functions.ILike(page.Username, pattern) || EF.Functions.ILike(page.Category, pattern));
        }

        if (name is not null)
        {
            query = query.Where(page => string.Compare(page.Name, name) > 0 ||
                page.Name == name && page.Id.CompareTo(id!.Value) > 0);
        }

        var pages = await query.OrderBy(page => page.Name).ThenBy(page => page.Id).Take(limit + 1).ToListAsync(cancellationToken);
        var visible = pages.Take(limit).ToList();
        var nextCursor = pages.Count > limit ? EncodeNameCursor(visible[^1]) : null;
        return ApplicationResult<PageCursorPageResponse<PageResponse>>.Success(new PageCursorPageResponse<PageResponse>(
            await ToResponsesAsync(visible, viewerUserId, cancellationToken), nextCursor));
    }

    public async Task<ApplicationResult<PageCursorPageResponse<PageMemberResponse>>> GetMembersAsync(Guid actorUserId, Guid pageId,
        string? cursor, int limit, CancellationToken cancellationToken = default)
    {
        if (!TryPagination(limit, out var error) || !TryDecodeTimelineCursor(cursor, out var joinedAtUtc, out var id))
        {
            return ApplicationResult<PageCursorPageResponse<PageMemberResponse>>.Failure(error ?? InvalidCursor());
        }

        if (!await pagePostAccessService.IsMemberAsync(pageId, actorUserId, cancellationToken))
        {
            return Forbidden<PageCursorPageResponse<PageMemberResponse>>();
        }

        var query = dbContext.PageMembers.AsNoTracking().Where(member => member.PageId == pageId);
        if (joinedAtUtc is not null)
        {
            query = query.Where(member => member.JoinedAtUtc < joinedAtUtc ||
                member.JoinedAtUtc == joinedAtUtc && member.UserId.CompareTo(id!.Value) < 0);
        }

        var rows = await (from member in query
                          join profile in dbContext.UserProfiles.AsNoTracking() on member.UserId equals profile.UserId
                          select new PageMemberResponse(member.UserId, profile.Username, profile.DisplayName,
                              RoleName(member.Role), member.JoinedAtUtc))
            .OrderByDescending(item => item.JoinedAtUtc).ThenByDescending(item => item.UserId).Take(limit + 1)
            .ToListAsync(cancellationToken);
        var items = rows.Take(limit).ToList();
        var next = rows.Count > limit ? EncodeTimelineCursor(items[^1].JoinedAtUtc, items[^1].UserId) : null;
        return ApplicationResult<PageCursorPageResponse<PageMemberResponse>>.Success(new(items, next));
    }

    public async Task<ApplicationResult<PageRoleInvitationResponse>> InviteAsync(Guid actorUserId, Guid pageId,
        CreatePageRoleInvitationRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryParseAssignableRole(request.Role, out var requestedRole))
        {
            return Validation<PageRoleInvitationResponse>("invalid_page_role", "Role must be admin, editor, or moderator.");
        }

        var actorRole = await pagePostAccessService.GetRoleAsync(pageId, actorUserId, cancellationToken);
        if (!CanManageRole(actorRole, requestedRole))
        {
            return Forbidden<PageRoleInvitationResponse>();
        }

        var activePage = await dbContext.Pages.AsNoTracking().AnyAsync(page => page.Id == pageId && page.DeletedAtUtc == null, cancellationToken);
        if (!activePage)
        {
            return NotFound<PageRoleInvitationResponse>();
        }

        if (!await dbContext.Users.AnyAsync(user => user.Id == request.UserId, cancellationToken))
        {
            return Validation<PageRoleInvitationResponse>("invitee_not_found", "The invited user was not found.");
        }

        if (await pagePostAccessService.IsMemberAsync(pageId, request.UserId, cancellationToken))
        {
            return Conflict<PageRoleInvitationResponse>("already_page_member", "This user already manages the Page.");
        }

        if (await dbContext.PageRoleInvitations.AnyAsync(invitation => invitation.PageId == pageId &&
                invitation.InviteeUserId == request.UserId && invitation.Status == PageRoleInvitationStatus.PENDING,
                cancellationToken))
        {
            return Conflict<PageRoleInvitationResponse>("page_invitation_pending", "A Page role invitation is already pending for this user.");
        }

        var now = timeProvider.GetUtcNow();
        var invitation = PageRoleInvitation.Create(Guid.NewGuid(), pageId, actorUserId, request.UserId, requestedRole, now);
        dbContext.PageRoleInvitations.Add(invitation);
        var notification = await notificationService.QueueAsync(request.UserId, actorUserId, NotificationType.PAGE_ROLE_INVITE,
            NotificationEntityType.PAGE_ROLE_INVITATION, invitation.Id, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (notification is not null)
        {
            await notificationService.PublishAsync(notification, cancellationToken);
        }

        return ApplicationResult<PageRoleInvitationResponse>.Success(ToResponse(invitation));
    }

    public async Task<ApplicationResult<PageCursorPageResponse<PageRoleInvitationResponse>>> GetMyInvitationsAsync(Guid actorUserId,
        string? cursor, int limit, CancellationToken cancellationToken = default)
    {
        if (!TryPagination(limit, out var error) || !TryDecodeTimelineCursor(cursor, out var createdAtUtc, out var id))
        {
            return ApplicationResult<PageCursorPageResponse<PageRoleInvitationResponse>>.Failure(error ?? InvalidCursor());
        }

        var query = dbContext.PageRoleInvitations.AsNoTracking().Where(invitation =>
            invitation.InviteeUserId == actorUserId && invitation.Status == PageRoleInvitationStatus.PENDING);
        if (createdAtUtc is not null)
        {
            query = query.Where(invitation => invitation.CreatedAtUtc < createdAtUtc ||
                invitation.CreatedAtUtc == createdAtUtc && invitation.Id.CompareTo(id!.Value) < 0);
        }

        var invitations = await query.OrderByDescending(invitation => invitation.CreatedAtUtc).ThenByDescending(invitation => invitation.Id)
            .Take(limit + 1).ToListAsync(cancellationToken);
        var items = invitations.Take(limit).Select(ToResponse).ToList();
        var next = invitations.Count > limit ? EncodeTimelineCursor(items[^1].CreatedAtUtc, items[^1].Id) : null;
        return ApplicationResult<PageCursorPageResponse<PageRoleInvitationResponse>>.Success(new(items, next));
    }

    public async Task<ApplicationResult<PageRoleInvitationResponse>> AcceptInvitationAsync(Guid actorUserId, Guid invitationId,
        CancellationToken cancellationToken = default) =>
        await RespondInvitationAsync(actorUserId, invitationId, true, cancellationToken);

    public async Task<ApplicationResult<PageRoleInvitationResponse>> DeclineInvitationAsync(Guid actorUserId, Guid invitationId,
        CancellationToken cancellationToken = default) =>
        await RespondInvitationAsync(actorUserId, invitationId, false, cancellationToken);

    public async Task<ApplicationResult<PageMemberResponse>> ChangeMemberRoleAsync(Guid actorUserId, Guid pageId, Guid userId,
        ChangePageMemberRoleRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryParseAssignableRole(request.Role, out var requestedRole))
        {
            return Validation<PageMemberResponse>("invalid_page_role", "Role must be admin, editor, or moderator.");
        }

        var actorRole = await pagePostAccessService.GetRoleAsync(pageId, actorUserId, cancellationToken);
        if (!CanManageRole(actorRole, requestedRole))
        {
            return Forbidden<PageMemberResponse>();
        }

        var member = await dbContext.PageMembers.SingleOrDefaultAsync(item => item.PageId == pageId && item.UserId == userId, cancellationToken);
        if (member is null)
        {
            return NotFound<PageMemberResponse>();
        }

        if (member.Role == PageRole.OWNER || actorRole == PageRole.ADMIN && member.Role == PageRole.ADMIN)
        {
            return Forbidden<PageMemberResponse>();
        }

        member.ChangeRole(requestedRole, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        var profile = await dbContext.UserProfiles.AsNoTracking().SingleAsync(profile => profile.UserId == userId, cancellationToken);
        return ApplicationResult<PageMemberResponse>.Success(new(userId, profile.Username, profile.DisplayName,
            RoleName(member.Role), member.JoinedAtUtc));
    }

    public async Task<ApplicationResult> RemoveMemberAsync(Guid actorUserId, Guid pageId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var actorRole = await pagePostAccessService.GetRoleAsync(pageId, actorUserId, cancellationToken);
        var member = await dbContext.PageMembers.SingleOrDefaultAsync(item => item.PageId == pageId && item.UserId == userId, cancellationToken);
        if (member is null)
        {
            return NotFound();
        }

        if (member.Role == PageRole.OWNER || !CanRemoveMember(actorRole, member.Role))
        {
            return Forbidden();
        }

        dbContext.PageMembers.Remove(member);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult> TransferOwnershipAsync(Guid actorUserId, Guid pageId,
        TransferPageOwnershipRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await dbContext.PageMembers.SingleOrDefaultAsync(member => member.PageId == pageId && member.UserId == actorUserId,
            cancellationToken);
        if (actor?.Role != PageRole.OWNER)
        {
            return Forbidden();
        }

        var target = await dbContext.PageMembers.SingleOrDefaultAsync(member => member.PageId == pageId && member.UserId == request.UserId,
            cancellationToken);
        if (target is null)
        {
            return Validation("new_owner_must_be_member", "The new owner must already manage this Page.");
        }

        if (target.UserId == actorUserId)
        {
            return ApplicationResult.Success();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow();
            actor.ChangeRole(PageRole.ADMIN, now);
            target.ChangeRole(PageRole.OWNER, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ApplicationResult.Success();
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<ApplicationResult<PageTimelineResponse>> GetPostsAsync(Guid pageId, Guid? viewerUserId, string? cursor,
        int limit, CancellationToken cancellationToken = default)
    {
        if (!TryPagination(limit, out var error) || !TryDecodeTimelineCursor(cursor, out var createdAtUtc, out var id))
        {
            return ApplicationResult<PageTimelineResponse>.Failure(error ?? InvalidCursor());
        }

        if (!await pagePostAccessService.CanViewPageAsync(pageId, viewerUserId, cancellationToken))
        {
            return NotFound<PageTimelineResponse>();
        }

        var query = dbContext.Posts.AsNoTracking().Where(post => post.ContainerType == PostContainerType.PAGE &&
            post.ContainerId == pageId && post.PostType == PostType.STANDARD && post.DeletedAtUtc == null);
        if (createdAtUtc is not null)
        {
            query = query.Where(post => post.CreatedAtUtc < createdAtUtc ||
                post.CreatedAtUtc == createdAtUtc && post.Id.CompareTo(id!.Value) < 0);
        }

        var posts = await query.OrderByDescending(post => post.CreatedAtUtc).ThenByDescending(post => post.Id)
            .Take(limit + 1).ToListAsync(cancellationToken);
        var visible = posts.Take(limit).ToList();
        var next = posts.Count > limit ? EncodeTimelineCursor(visible[^1].CreatedAtUtc, visible[^1].Id) : null;
        return ApplicationResult<PageTimelineResponse>.Success(new(
            await postsService.LoadResponsesAsync(visible, viewerUserId, cancellationToken), next));
    }

    public async Task<ApplicationResult<PostResponse>> CreatePostAsync(Guid actorUserId, Guid pageId, CreatePostRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await pagePostAccessService.CanCreatePostAsync(pageId, actorUserId, cancellationToken))
        {
            return Forbidden<PostResponse>();
        }

        return await postsUseCase.CreatePagePostAsync(actorUserId, pageId, request.Content, request.MediaIds ?? [], cancellationToken);
    }

    public async Task<ApplicationResult<Guid>> GetMediaIdAsync(Guid pageId, PageMediaSlot slot, Guid? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var page = await dbContext.Pages.AsNoTracking().SingleOrDefaultAsync(item => item.Id == pageId && item.DeletedAtUtc == null,
            cancellationToken);
        if (page is null || !await pagePostAccessService.CanViewPageAsync(pageId, viewerUserId, cancellationToken))
        {
            return NotFound<Guid>();
        }

        var mediaId = slot == PageMediaSlot.AVATAR ? page.AvatarMediaId : page.CoverMediaId;
        return mediaId is null ? NotFound<Guid>() : ApplicationResult<Guid>.Success(mediaId.Value);
    }

    private async Task<ApplicationResult<PageResponse>> ChangeStatusAsync(Guid actorUserId, Guid pageId, bool publish,
        CancellationToken cancellationToken)
    {
        var page = await dbContext.Pages.SingleOrDefaultAsync(item => item.Id == pageId && item.DeletedAtUtc == null, cancellationToken);
        if (page is null)
        {
            return NotFound<PageResponse>();
        }

        if (await pagePostAccessService.GetRoleAsync(pageId, actorUserId, cancellationToken) != PageRole.OWNER)
        {
            return Forbidden<PageResponse>();
        }

        if (publish) page.Publish(timeProvider.GetUtcNow());
        else page.Unpublish(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult<PageResponse>.Success(await ToResponseAsync(page, actorUserId, PageRole.OWNER, cancellationToken));
    }

    private async Task<ApplicationResult<PageRoleInvitationResponse>> RespondInvitationAsync(Guid actorUserId, Guid invitationId,
        bool accept, CancellationToken cancellationToken)
    {
        var invitation = await dbContext.PageRoleInvitations.SingleOrDefaultAsync(item => item.Id == invitationId &&
            item.InviteeUserId == actorUserId && item.Status == PageRoleInvitationStatus.PENDING, cancellationToken);
        if (invitation is null)
        {
            return NotFound<PageRoleInvitationResponse>();
        }

        if (!await dbContext.Pages.AsNoTracking().AnyAsync(page => page.Id == invitation.PageId && page.DeletedAtUtc == null,
                cancellationToken))
        {
            return NotFound<PageRoleInvitationResponse>();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow();
            if (accept)
            {
                if (await pagePostAccessService.IsMemberAsync(invitation.PageId, actorUserId, cancellationToken))
                {
                    return Conflict<PageRoleInvitationResponse>("already_page_member", "You already manage this Page.");
                }

                dbContext.PageMembers.Add(PageMember.Create(invitation.PageId, actorUserId, invitation.Role, now));
                invitation.Accept(now);
            }
            else
            {
                invitation.Decline(now);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ApplicationResult<PageRoleInvitationResponse>.Success(ToResponse(invitation));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Conflict<PageRoleInvitationResponse>("already_page_member", "You already manage this Page.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<PageResponse> ToResponseAsync(Page page, Guid? viewerUserId, PageRole? role,
        CancellationToken cancellationToken)
    {
        var followerCount = await dbContext.PageFollowers.AsNoTracking().CountAsync(follower => follower.PageId == page.Id, cancellationToken);
        var isFollowing = viewerUserId is not null && await dbContext.PageFollowers.AsNoTracking().AnyAsync(follower =>
            follower.PageId == page.Id && follower.UserId == viewerUserId.Value, cancellationToken);
        return ToResponse(page, followerCount, isFollowing, role);
    }

    private async Task<IReadOnlyList<PageResponse>> ToResponsesAsync(IReadOnlyList<Page> pages, Guid? viewerUserId,
        CancellationToken cancellationToken)
    {
        if (pages.Count == 0) return [];
        var ids = pages.Select(page => page.Id).ToArray();
        var followers = await dbContext.PageFollowers.AsNoTracking().Where(follower => ids.Contains(follower.PageId))
            .GroupBy(follower => follower.PageId).Select(group => new { PageId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.PageId, item => item.Count, cancellationToken);
        var following = viewerUserId is null ? new HashSet<Guid>() : (await dbContext.PageFollowers.AsNoTracking()
            .Where(follower => follower.UserId == viewerUserId.Value && ids.Contains(follower.PageId))
            .Select(follower => follower.PageId).ToListAsync(cancellationToken)).ToHashSet();
        var roles = viewerUserId is null ? new Dictionary<Guid, PageRole>() : await dbContext.PageMembers.AsNoTracking()
            .Where(member => member.UserId == viewerUserId.Value && ids.Contains(member.PageId))
            .ToDictionaryAsync(member => member.PageId, member => member.Role, cancellationToken);
        return pages.Select(page => ToResponse(page, followers.GetValueOrDefault(page.Id), following.Contains(page.Id),
            roles.GetValueOrDefault(page.Id))).ToList();
    }

    private async Task<PageCursorPageResponse<PageResponse>> ToCursorPageAsync(IReadOnlyList<Page> pages, Guid viewerUserId,
        int limit, CancellationToken cancellationToken, IReadOnlyList<DateTimeOffset>? cursorDates = null)
    {
        var visible = pages.Take(limit).ToList();
        var responses = await ToResponsesAsync(visible, viewerUserId, cancellationToken);
        var nextCursor = pages.Count <= limit ? null : EncodeTimelineCursor(
            cursorDates is null ? visible[^1].CreatedAtUtc : cursorDates[limit - 1], visible[^1].Id);
        return new PageCursorPageResponse<PageResponse>(responses, nextCursor);
    }

    private static PageResponse ToResponse(Page page, int followerCount, bool isFollowing, PageRole? role) => new(
        page.Id, page.Name, page.Username, page.Category, page.Bio, StatusName(page.Status),
        page.AvatarMediaId is null ? null : $"/api/pages/{page.Id}/avatar",
        page.CoverMediaId is null ? null : $"/api/pages/{page.Id}/cover",
        followerCount, isFollowing, role is null ? null : RoleName(role.Value), page.CreatedAtUtc, page.UpdatedAtUtc);

    private static PageRoleInvitationResponse ToResponse(PageRoleInvitation invitation) => new(invitation.Id, invitation.PageId,
        invitation.InviterUserId, invitation.InviteeUserId, RoleName(invitation.Role), invitation.Status.ToString().ToLowerInvariant(),
        invitation.CreatedAtUtc, invitation.RespondedAtUtc);

    private async Task<Page?> FindPageAsync(string idOrUsername, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(idOrUsername, out var id))
        {
            return await dbContext.Pages.AsNoTracking().SingleOrDefaultAsync(page => page.Id == id && page.DeletedAtUtc == null,
                cancellationToken);
        }

        var username = idOrUsername.Trim().ToLowerInvariant();
        return await dbContext.Pages.AsNoTracking().SingleOrDefaultAsync(page => page.Username == username && page.DeletedAtUtc == null,
            cancellationToken);
    }

    private static bool TryValidatePage(string name, string username, string category, string? bio, out string normalizedUsername,
        out ApplicationError? error)
    {
        try
        {
            normalizedUsername = Page.NormalizeUsername(username);
            _ = Page.Create(Guid.Empty, name, normalizedUsername, category, bio, Guid.Empty, DateTimeOffset.UnixEpoch);
            if (ReservedUsernames.Contains(normalizedUsername))
            {
                error = new ApplicationError("reserved_page_username", "This Page username is reserved.", ApplicationErrorType.VALIDATION);
                return false;
            }
            error = null;
            return true;
        }
        catch (ArgumentException exception)
        {
            normalizedUsername = string.Empty;
            error = new ApplicationError("invalid_page", exception.Message, ApplicationErrorType.VALIDATION);
            return false;
        }
    }

    private static bool TryParseAssignableRole(string role, out PageRole parsed) =>
        Enum.TryParse(role, true, out parsed) && Enum.IsDefined(parsed) && parsed != PageRole.OWNER;

    private static bool CanManageRole(PageRole? actorRole, PageRole requestedRole) =>
        actorRole == PageRole.OWNER || actorRole == PageRole.ADMIN && requestedRole is PageRole.EDITOR or PageRole.MODERATOR;

    private static bool CanRemoveMember(PageRole? actorRole, PageRole targetRole) =>
        actorRole == PageRole.OWNER && targetRole != PageRole.OWNER ||
        actorRole == PageRole.ADMIN && targetRole is PageRole.EDITOR or PageRole.MODERATOR;

    private static string RoleName(PageRole role) => role.ToString().ToLowerInvariant();
    private static string StatusName(PageStatus status) => status.ToString().ToLowerInvariant();

    private static bool TryPagination(int limit, out ApplicationError? error)
    {
        error = limit is < 1 or > MaximumPageSize
            ? new ApplicationError(ErrorCode.InvalidPagination, $"Limit must be between 1 and {MaximumPageSize}.", ApplicationErrorType.VALIDATION)
            : null;
        return error is null;
    }

    private static string EncodeTimelineCursor(DateTimeOffset timestamp, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{timestamp.UtcDateTime.Ticks}:{id:D}"));

    private static bool TryDecodeTimelineCursor(string? cursor, out DateTimeOffset? timestamp, out Guid? id)
    {
        timestamp = null;
        id = null;
        if (string.IsNullOrWhiteSpace(cursor)) return true;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split(':');
            if (parts.Length != 2 || !long.TryParse(parts[0], out var ticks) || !Guid.TryParse(parts[1], out var parsedId)) return false;
            timestamp = new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc));
            id = parsedId;
            return true;
        }
        catch (FormatException) { return false; }
    }

    private static string EncodeNameCursor(Page page) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Convert.ToBase64String(Encoding.UTF8.GetBytes(page.Name))}:{page.Id:D}"));

    private static bool TryDecodeNameCursor(string? cursor, out string? name, out Guid? id)
    {
        name = null;
        id = null;
        if (string.IsNullOrWhiteSpace(cursor)) return true;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split(':');
            if (parts.Length != 2 || !Guid.TryParse(parts[1], out var parsedId)) return false;
            name = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
            id = parsedId;
            return true;
        }
        catch (FormatException) { return false; }
    }

    private static ApplicationError InvalidCursor() =>
        new("invalid_cursor", "The cursor is invalid.", ApplicationErrorType.VALIDATION);
    private static ApplicationResult NotFound() => ApplicationResult.Failure(new ApplicationError("page_not_found", "The Page was not found.", ApplicationErrorType.NOT_FOUND));
    private static ApplicationResult<T> NotFound<T>() => ApplicationResult<T>.Failure(new ApplicationError("page_not_found", "The Page was not found.", ApplicationErrorType.NOT_FOUND));
    private static ApplicationResult Forbidden() => ApplicationResult.Failure(new ApplicationError(ErrorCode.Forbidden, "You are not allowed to manage this Page.", ApplicationErrorType.FORBIDDEN));
    private static ApplicationResult<T> Forbidden<T>() => ApplicationResult<T>.Failure(new ApplicationError(ErrorCode.Forbidden, "You are not allowed to manage this Page.", ApplicationErrorType.FORBIDDEN));
    private static ApplicationResult<T> Validation<T>(string code, string message) => ApplicationResult<T>.Failure(new ApplicationError(code, message, ApplicationErrorType.VALIDATION));
    private static ApplicationResult Validation(string code, string message) => ApplicationResult.Failure(new ApplicationError(code, message, ApplicationErrorType.VALIDATION));
    private static ApplicationResult<T> Conflict<T>(string code, string message) => ApplicationResult<T>.Failure(new ApplicationError(code, message, ApplicationErrorType.CONFLICT));
}
