using System.Globalization;
using System.Text;
using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Groups.DTOs.Requests;
using Fookbase.Api.Modules.Groups.DTOs.Responses;
using Fookbase.Api.Modules.Groups.Entities;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Notifications.Entities;
using Fookbase.Api.Modules.Notifications.Services;
using Fookbase.Api.Modules.Posts.DTOs.Requests;
using Fookbase.Api.Modules.Posts.DTOs.Responses;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Groups.Services;

public sealed class GroupsService(
    FookbaseDbContext dbContext,
    GroupPostAccessService groupPostAccessService,
    FriendsService friendsService,
    MediaService mediaService,
    NotificationService notificationService,
    PostsUseCase postsUseCase,
    PostsService postsService,
    SocialInteractionsService socialInteractionsService,
    TimeProvider timeProvider)
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 50;

    public async Task<ApplicationResult<GroupResponse>> CreateAsync(
        Guid actorUserId,
        CreateGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryParsePrivacy(request.Privacy, out var privacy))
        {
            return Failure<GroupResponse>(
                "invalid_group_privacy",
                "Group privacy must be public or private.",
                ApplicationErrorType.Validation);
        }

        try
        {
            var now = timeProvider.GetUtcNow();
            var group = Group.Create(
                Guid.NewGuid(),
                request.Name,
                request.Description,
                privacy,
                actorUserId,
                now);
            var owner = GroupMember.Create(group.Id, actorUserId, GroupMemberRole.Owner, now);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            dbContext.Groups.Add(group);
            dbContext.GroupMembers.Add(owner);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ApplicationResult<GroupResponse>.Success(ToResponse(group, 1, owner.Role));
        }
        catch (ArgumentException exception)
        {
            return Failure<GroupResponse>("invalid_group", exception.Message, ApplicationErrorType.Validation);
        }
    }

    public async Task<ApplicationResult<GroupResponse>> GetAsync(
        Guid groupId,
        Guid? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var group = await FindActiveGroupAsync(groupId, cancellationToken);
        if (group is null ||
            !await groupPostAccessService.CanViewGroupAsync(group, viewerUserId, cancellationToken))
        {
            return NotFound<GroupResponse>();
        }

        var memberCount = await dbContext.GroupMembers.AsNoTracking()
            .CountAsync(member => member.GroupId == groupId, cancellationToken);
        var viewerRole = viewerUserId is null
            ? null
            : await dbContext.GroupMembers.AsNoTracking()
                .Where(member => member.GroupId == groupId && member.UserId == viewerUserId.Value)
                .Select(member => (GroupMemberRole?)member.Role)
                .SingleOrDefaultAsync(cancellationToken);
        return ApplicationResult<GroupResponse>.Success(ToResponse(group, memberCount, viewerRole));
    }

    public async Task<ApplicationResult<GroupCursorPageResponse<GroupResponse>>> GetMineAsync(
        Guid actorUserId,
        string? cursorValue,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidCursor(cursorValue) || !IsValidLimit(limit))
        {
            return InvalidPage<GroupCursorPageResponse<GroupResponse>>();
        }

        var cursor = DecodeCursorOrNull(cursorValue);
        var query =
            from membership in dbContext.GroupMembers.AsNoTracking()
            join itemGroup in dbContext.Groups.AsNoTracking() on membership.GroupId equals itemGroup.Id
            where membership.UserId == actorUserId && itemGroup.DeletedAtUtc == null
            select new { Group = itemGroup, membership.Role };
        if (cursor is not null)
        {
            query = query.Where(item =>
                item.Group.CreatedAtUtc < cursor.CreatedAtUtc ||
                (item.Group.CreatedAtUtc == cursor.CreatedAtUtc &&
                 item.Group.Id.CompareTo(cursor.Id) < 0));
        }

        var candidates = await query
            .OrderByDescending(item => item.Group.CreatedAtUtc)
            .ThenByDescending(item => item.Group.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        var page = candidates.Take(limit).ToList();
        var counts = await LoadMemberCountsAsync(page.Select(item => item.Group.Id), cancellationToken);
        return ApplicationResult<GroupCursorPageResponse<GroupResponse>>.Success(
            new GroupCursorPageResponse<GroupResponse>(
                page.Select(item => ToResponse(
                    item.Group,
                    counts.GetValueOrDefault(item.Group.Id),
                    item.Role)).ToList(),
                candidates.Count > limit ? EncodeCursor(page[^1].Group) : null));
    }

    public async Task<ApplicationResult<GroupCursorPageResponse<GroupResponse>>> DiscoverAsync(
        string? queryText,
        string? cursorValue,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidCursor(cursorValue) || !IsValidLimit(limit))
        {
            return InvalidPage<GroupCursorPageResponse<GroupResponse>>();
        }

        var cursor = DecodeCursorOrNull(cursorValue);
        var normalizedQuery = queryText?.Trim().ToLowerInvariant();
        var query = dbContext.Groups.AsNoTracking().Where(group =>
            group.DeletedAtUtc == null && group.Privacy == GroupPrivacy.Public);
        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            query = query.Where(group =>
                group.Name.ToLower().Contains(normalizedQuery) ||
                (group.Description != null && group.Description.ToLower().Contains(normalizedQuery)));
        }

        if (cursor is not null)
        {
            query = query.Where(group =>
                group.CreatedAtUtc < cursor.CreatedAtUtc ||
                (group.CreatedAtUtc == cursor.CreatedAtUtc &&
                 group.Id.CompareTo(cursor.Id) < 0));
        }

        var candidates = await query
            .OrderByDescending(group => group.CreatedAtUtc)
            .ThenByDescending(group => group.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        var page = candidates.Take(limit).ToList();
        var counts = await LoadMemberCountsAsync(page.Select(group => group.Id), cancellationToken);
        return ApplicationResult<GroupCursorPageResponse<GroupResponse>>.Success(
            new GroupCursorPageResponse<GroupResponse>(
                page.Select(group => ToResponse(group, counts.GetValueOrDefault(group.Id), null)).ToList(),
                candidates.Count > limit ? EncodeCursor(page[^1]) : null));
    }

    public async Task<ApplicationResult<GroupCursorPageResponse<GroupInviteResponse>>> GetMyInvitesAsync(
        Guid actorUserId,
        string? cursorValue,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidCursor(cursorValue) || !IsValidLimit(limit))
        {
            return InvalidPage<GroupCursorPageResponse<GroupInviteResponse>>();
        }

        var cursor = DecodeCursorOrNull(cursorValue);
        var query =
            from invite in dbContext.GroupInvites.AsNoTracking()
            join itemGroup in dbContext.Groups.AsNoTracking() on invite.GroupId equals itemGroup.Id
            where invite.InviteeUserId == actorUserId &&
                  invite.Status == GroupInviteStatus.Pending &&
                  itemGroup.DeletedAtUtc == null
            select new { Invite = invite, Group = itemGroup };
        if (cursor is not null)
        {
            query = query.Where(item =>
                item.Invite.CreatedAtUtc < cursor.CreatedAtUtc ||
                (item.Invite.CreatedAtUtc == cursor.CreatedAtUtc &&
                 item.Invite.Id.CompareTo(cursor.Id) < 0));
        }

        var candidates = await query
            .OrderByDescending(item => item.Invite.CreatedAtUtc)
            .ThenByDescending(item => item.Invite.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        var page = candidates.Take(limit).ToList();
        var counts = await LoadMemberCountsAsync(page.Select(item => item.Group.Id), cancellationToken);
        return ApplicationResult<GroupCursorPageResponse<GroupInviteResponse>>.Success(
            new GroupCursorPageResponse<GroupInviteResponse>(
                page.Select(item => ToResponse(
                    item.Invite,
                    ToResponse(item.Group, counts.GetValueOrDefault(item.Group.Id), null))).ToList(),
                candidates.Count > limit ? EncodeCursor(page[^1].Invite.CreatedAtUtc, page[^1].Invite.Id) : null));
    }

    public async Task<ApplicationResult<GroupFeedPageResponse>> GetFeedAsync(
        Guid actorUserId,
        string? cursorValue,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidCursor(cursorValue) || !IsValidLimit(limit))
        {
            return InvalidPage<GroupFeedPageResponse>();
        }

        var cursor = DecodeCursorOrNull(cursorValue);
        var blockedUserIds = (await friendsService.GetAccessSnapshotAsync(actorUserId, cancellationToken)).BlockedUserIds;
        var joinedGroupIds = dbContext.GroupMembers.AsNoTracking()
            .Where(member => member.UserId == actorUserId)
            .Select(member => member.GroupId);
        var query = dbContext.Posts.AsNoTracking().Where(post =>
            post.DeletedAtUtc == null &&
            post.ContainerType == PostContainerType.Group &&
            joinedGroupIds.Contains(post.ContainerId) &&
            !blockedUserIds.Contains(post.AuthorUserId));
        if (cursor is not null)
        {
            query = query.Where(post =>
                post.CreatedAtUtc < cursor.CreatedAtUtc ||
                (post.CreatedAtUtc == cursor.CreatedAtUtc && post.Id.CompareTo(cursor.Id) < 0));
        }

        var candidates = await query
            .OrderByDescending(post => post.CreatedAtUtc)
            .ThenByDescending(post => post.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        var page = candidates.Take(limit).ToList();
        var groupIds = page.Select(post => post.ContainerId).Distinct().ToArray();
        var groups = await dbContext.Groups.AsNoTracking()
            .Where(group => groupIds.Contains(group.Id) && group.DeletedAtUtc == null)
            .ToDictionaryAsync(group => group.Id, cancellationToken);
        var counts = await LoadMemberCountsAsync(groupIds, cancellationToken);
        var roles = await dbContext.GroupMembers.AsNoTracking()
            .Where(member => member.UserId == actorUserId && groupIds.Contains(member.GroupId))
            .ToDictionaryAsync(member => member.GroupId, member => member.Role, cancellationToken);
        var posts = await postsService.LoadResponsesAsync(page, actorUserId, cancellationToken);
        var items = posts.Zip(page, (post, entity) => new { Post = post, GroupId = entity.ContainerId })
            .Where(item => groups.ContainsKey(item.GroupId))
            .Select(item => new GroupFeedItemResponse(
                ToResponse(groups[item.GroupId], counts.GetValueOrDefault(item.GroupId), roles.GetValueOrDefault(item.GroupId)),
                item.Post))
            .ToList();
        return ApplicationResult<GroupFeedPageResponse>.Success(new GroupFeedPageResponse(
            items,
            candidates.Count > limit ? EncodeCursor(page[^1]) : null));
    }

    public async Task<ApplicationResult<GroupResponse>> UpdateAsync(
        Guid actorUserId,
        Guid groupId,
        UpdateGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var group = await FindActiveGroupAsync(groupId, cancellationToken);
        if (group is null)
        {
            return NotFound<GroupResponse>();
        }

        var actorRole = await GetMemberRoleAsync(groupId, actorUserId, cancellationToken);
        if (!CanManageGroup(actorRole))
        {
            return Forbidden<GroupResponse>();
        }

        if (!TryParsePrivacy(request.Privacy, out var privacy))
        {
            return Failure<GroupResponse>(
                "invalid_group_privacy",
                "Group privacy must be public or private.",
                ApplicationErrorType.Validation);
        }

        var coverChanged = !request.RemoveCover && request.CoverMediaId is { } requestedCoverMediaId &&
            requestedCoverMediaId != group.CoverMediaId;
        var desiredCoverMediaId = request.RemoveCover ? null : request.CoverMediaId ?? group.CoverMediaId;
        if (request.CoverMediaId is not null)
        {
            var media = await mediaService.ValidateGroupCoverImageAsync(
                actorUserId,
                request.CoverMediaId.Value,
                cancellationToken);
            if (!media.Succeeded)
            {
                return ApplicationResult<GroupResponse>.Failure(media.Error!);
            }
        }

        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            group.Update(request.Name, request.Description, privacy, timeProvider.GetUtcNow());
            group.SetCover(desiredCoverMediaId, timeProvider.GetUtcNow());
            await mediaService.SynchronizeGroupCoverReferenceAsync(
                group.Id,
                desiredCoverMediaId,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            if (coverChanged)
            {
                var post = await postsService.CreatePostInContainerCoreAsync(
                    actorUserId,
                    Post.CoverUpdatedPostContent,
                    PostPrivacy.Public,
                    PostContainerType.Group,
                    group.Id,
                    [request.CoverMediaId!.Value],
                    cancellationToken,
                    addToTimelinePhotos: false);
                if (!post.Succeeded)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure<GroupResponse>("group_cover_post_failed", "Could not create the group cover update post.", ApplicationErrorType.Conflict);
                }

                var references = await mediaService.SynchronizePostReferencesAsync(
                    actorUserId,
                    post.Value!.Id,
                    [request.CoverMediaId!.Value],
                    cancellationToken);
                if (!references.Succeeded)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ApplicationResult<GroupResponse>.Failure(references.Error!);
                }

                await socialInteractionsService.SynchronizePostMetadataAsync(
                    post.Value.Id,
                    actorUserId,
                    cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return Failure<GroupResponse>("invalid_group", exception.Message, ApplicationErrorType.Validation);
        }

        var count = await dbContext.GroupMembers.AsNoTracking()
            .CountAsync(member => member.GroupId == groupId, cancellationToken);
        return ApplicationResult<GroupResponse>.Success(ToResponse(group, count, actorRole));
    }

    public async Task<ApplicationResult> DeleteAsync(
        Guid actorUserId,
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var group = await FindActiveGroupAsync(groupId, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        if (await GetMemberRoleAsync(groupId, actorUserId, cancellationToken) != GroupMemberRole.Owner)
        {
            return Forbidden();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        group.Delete(timeProvider.GetUtcNow());
        await mediaService.SynchronizeGroupCoverReferenceAsync(groupId, null, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult<GroupJoinRequestResponse?>> JoinAsync(
        Guid actorUserId,
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var group = await FindActiveGroupAsync(groupId, cancellationToken);
        if (group is null)
        {
            return NotFound<GroupJoinRequestResponse?>();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (await dbContext.GroupMembers.AnyAsync(
                member => member.GroupId == groupId && member.UserId == actorUserId,
                cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Failure<GroupJoinRequestResponse?>(
                "already_group_member",
                "The user is already a group member.",
                ApplicationErrorType.Conflict);
        }

        if (group.Privacy == GroupPrivacy.Public)
        {
            dbContext.GroupMembers.Add(GroupMember.Create(
                groupId,
                actorUserId,
                GroupMemberRole.Member,
                timeProvider.GetUtcNow()));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ApplicationResult<GroupJoinRequestResponse?>.Success(null);
        }

        if (await dbContext.GroupJoinRequests.AnyAsync(
                request =>
                    request.GroupId == groupId &&
                    request.RequesterUserId == actorUserId &&
                    request.Status == GroupJoinRequestStatus.Pending,
                cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Failure<GroupJoinRequestResponse?>(
                "pending_group_join_request",
                "A pending join request already exists.",
                ApplicationErrorType.Conflict);
        }

        var joinRequest = GroupJoinRequest.Create(
            Guid.NewGuid(),
            groupId,
            actorUserId,
            timeProvider.GetUtcNow());
        dbContext.GroupJoinRequests.Add(joinRequest);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ApplicationResult<GroupJoinRequestResponse?>.Success(ToResponse(joinRequest));
    }

    public async Task<ApplicationResult> LeaveAsync(
        Guid actorUserId,
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var membership = await dbContext.GroupMembers.SingleOrDefaultAsync(
            member => member.GroupId == groupId && member.UserId == actorUserId,
            cancellationToken);
        if (membership is null || await FindActiveGroupAsync(groupId, cancellationToken) is null)
        {
            return NotFound();
        }

        if (membership.Role == GroupMemberRole.Owner)
        {
            return Failure(
                "owner_cannot_leave",
                "Transfer ownership or delete the group before leaving.",
                ApplicationErrorType.Conflict);
        }

        dbContext.GroupMembers.Remove(membership);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult<GroupCursorPageResponse<GroupMemberResponse>>> GetMembersAsync(
        Guid groupId,
        Guid? viewerUserId,
        string? cursorValue,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var group = await FindActiveGroupAsync(groupId, cancellationToken);
        if (group is null ||
            !await groupPostAccessService.CanViewGroupAsync(group, viewerUserId, cancellationToken))
        {
            return NotFound<GroupCursorPageResponse<GroupMemberResponse>>();
        }

        if (!IsValidCursor(cursorValue) || !IsValidLimit(limit))
        {
            return InvalidPage<GroupCursorPageResponse<GroupMemberResponse>>();
        }

        var cursor = DecodeCursorOrNull(cursorValue);
        var query = dbContext.GroupMembers.AsNoTracking().Where(member => member.GroupId == groupId);
        if (cursor is not null)
        {
            query = query.Where(member =>
                member.JoinedAtUtc < cursor.CreatedAtUtc ||
                (member.JoinedAtUtc == cursor.CreatedAtUtc &&
                 member.UserId.CompareTo(cursor.Id) < 0));
        }

        var candidates = await query
            .OrderByDescending(member => member.JoinedAtUtc)
            .ThenByDescending(member => member.UserId)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        var page = candidates.Take(limit).ToList();
        var memberUserIds = page.Select(member => member.UserId).ToArray();
        var profiles = memberUserIds.Length == 0
            ? new Dictionary<Guid, GroupMemberProfile>()
            : await dbContext.UserProfiles.AsNoTracking()
                .Where(profile => memberUserIds.Contains(profile.UserId))
                .Select(profile => new GroupMemberProfile(
                    profile.UserId,
                    profile.Username,
                    profile.DisplayName,
                    profile.AvatarMediaId == null ? profile.AvatarUrl : $"/api/users/{profile.UserId}/avatar"))
                .ToDictionaryAsync(profile => profile.UserId, cancellationToken);
        return ApplicationResult<GroupCursorPageResponse<GroupMemberResponse>>.Success(
            new GroupCursorPageResponse<GroupMemberResponse>(
                page.Select(member => ToResponse(member, profiles.GetValueOrDefault(member.UserId))).ToList(),
                candidates.Count > limit ? EncodeCursor(page[^1].JoinedAtUtc, page[^1].UserId) : null));
    }

    public async Task<ApplicationResult<GroupCursorPageResponse<GroupJoinRequestResponse>>> GetJoinRequestsAsync(
        Guid actorUserId,
        Guid groupId,
        string? cursorValue,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!await CanModerateAsync(actorUserId, groupId, cancellationToken))
        {
            return Forbidden<GroupCursorPageResponse<GroupJoinRequestResponse>>();
        }

        if (!IsValidCursor(cursorValue) || !IsValidLimit(limit))
        {
            return InvalidPage<GroupCursorPageResponse<GroupJoinRequestResponse>>();
        }

        var cursor = DecodeCursorOrNull(cursorValue);
        var query = dbContext.GroupJoinRequests.AsNoTracking().Where(request =>
            request.GroupId == groupId && request.Status == GroupJoinRequestStatus.Pending);
        if (cursor is not null)
        {
            query = query.Where(request =>
                request.CreatedAtUtc < cursor.CreatedAtUtc ||
                (request.CreatedAtUtc == cursor.CreatedAtUtc &&
                 request.Id.CompareTo(cursor.Id) < 0));
        }

        var candidates = await query
            .OrderByDescending(request => request.CreatedAtUtc)
            .ThenByDescending(request => request.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        var page = candidates.Take(limit).ToList();
        return ApplicationResult<GroupCursorPageResponse<GroupJoinRequestResponse>>.Success(
            new GroupCursorPageResponse<GroupJoinRequestResponse>(
                page.Select(ToResponse).ToList(),
                candidates.Count > limit ? EncodeCursor(page[^1].CreatedAtUtc, page[^1].Id) : null));
    }

    public async Task<ApplicationResult<GroupJoinRequestResponse>> ApproveJoinRequestAsync(
        Guid actorUserId,
        Guid groupId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (!await CanModerateAsync(actorUserId, groupId, cancellationToken))
        {
            return Forbidden<GroupJoinRequestResponse>();
        }

        var request = await dbContext.GroupJoinRequests.SingleOrDefaultAsync(
            item =>
                item.Id == requestId &&
                item.GroupId == groupId &&
                item.Status == GroupJoinRequestStatus.Pending,
            cancellationToken);
        if (request is null)
        {
            return NotFound<GroupJoinRequestResponse>();
        }

        if (await dbContext.GroupMembers.AnyAsync(
                member => member.GroupId == groupId && member.UserId == request.RequesterUserId,
                cancellationToken))
        {
            return Failure<GroupJoinRequestResponse>(
                "already_group_member",
                "The requester is already a group member.",
                ApplicationErrorType.Conflict);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        request.Approve(actorUserId, now);
        dbContext.GroupMembers.Add(GroupMember.Create(
            groupId,
            request.RequesterUserId,
            GroupMemberRole.Member,
            now));
        var notification = await notificationService.QueueAsync(
            request.RequesterUserId,
            actorUserId,
            NotificationType.GroupJoinApproved,
            NotificationEntityType.GroupJoinRequest,
            request.Id,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (notification is not null)
        {
            await notificationService.PublishAsync(notification, cancellationToken);
        }

        return ApplicationResult<GroupJoinRequestResponse>.Success(ToResponse(request));
    }

    public async Task<ApplicationResult<GroupJoinRequestResponse>> DeclineJoinRequestAsync(
        Guid actorUserId,
        Guid groupId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (!await CanModerateAsync(actorUserId, groupId, cancellationToken))
        {
            return Forbidden<GroupJoinRequestResponse>();
        }

        var request = await dbContext.GroupJoinRequests.SingleOrDefaultAsync(
            item =>
                item.Id == requestId &&
                item.GroupId == groupId &&
                item.Status == GroupJoinRequestStatus.Pending,
            cancellationToken);
        if (request is null)
        {
            return NotFound<GroupJoinRequestResponse>();
        }

        request.Decline(actorUserId, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult<GroupJoinRequestResponse>.Success(ToResponse(request));
    }

    public async Task<ApplicationResult<GroupInviteResponse>> InviteAsync(
        Guid actorUserId,
        Guid groupId,
        CreateGroupInviteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.UserId == actorUserId)
        {
            return Failure<GroupInviteResponse>(
                "invalid_group_invitee",
                "You cannot invite yourself.",
                ApplicationErrorType.Validation);
        }

        var group = await FindActiveGroupAsync(groupId, cancellationToken);
        if (group is null)
        {
            return NotFound<GroupInviteResponse>();
        }

        if (await GetMemberRoleAsync(groupId, actorUserId, cancellationToken) is null)
        {
            return Forbidden<GroupInviteResponse>();
        }

        if (!await dbContext.Users.AsNoTracking().AnyAsync(user => user.Id == request.UserId, cancellationToken))
        {
            return Failure<GroupInviteResponse>(
                "group_invitee_not_found",
                "The invited user does not exist.",
                ApplicationErrorType.NotFound);
        }

        if (await dbContext.GroupMembers.AnyAsync(
                member => member.GroupId == groupId && member.UserId == request.UserId,
                cancellationToken))
        {
            return Failure<GroupInviteResponse>(
                "already_group_member",
                "The invited user is already a group member.",
                ApplicationErrorType.Conflict);
        }

        if (await dbContext.GroupInvites.AnyAsync(
                invite =>
                    invite.GroupId == groupId &&
                    invite.InviteeUserId == request.UserId &&
                    invite.Status == GroupInviteStatus.Pending,
                cancellationToken))
        {
            return Failure<GroupInviteResponse>(
                "pending_group_invite",
                "A pending group invite already exists.",
                ApplicationErrorType.Conflict);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var invite = GroupInvite.Create(
            Guid.NewGuid(),
            groupId,
            actorUserId,
            request.UserId,
            timeProvider.GetUtcNow());
        dbContext.GroupInvites.Add(invite);
        var notification = await notificationService.QueueAsync(
            request.UserId,
            actorUserId,
            NotificationType.GroupInvite,
            NotificationEntityType.GroupInvite,
            invite.Id,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (notification is not null)
        {
            await notificationService.PublishAsync(notification, cancellationToken);
        }

        return ApplicationResult<GroupInviteResponse>.Success(ToResponse(invite));
    }

    public async Task<ApplicationResult<GroupInviteResponse>> AcceptInviteAsync(
        Guid actorUserId,
        Guid groupId,
        Guid inviteId,
        CancellationToken cancellationToken = default) =>
        await RespondToInviteAsync(actorUserId, groupId, inviteId, true, cancellationToken);

    public async Task<ApplicationResult<GroupInviteResponse>> DeclineInviteAsync(
        Guid actorUserId,
        Guid groupId,
        Guid inviteId,
        CancellationToken cancellationToken = default) =>
        await RespondToInviteAsync(actorUserId, groupId, inviteId, false, cancellationToken);

    public async Task<ApplicationResult<GroupMemberResponse>> ChangeMemberRoleAsync(
        Guid actorUserId,
        Guid groupId,
        Guid targetUserId,
        ChangeGroupMemberRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseRole(request.Role, out var targetRole))
        {
            return Failure<GroupMemberResponse>(
                "invalid_group_role",
                "Group role must be owner, admin, moderator or member.",
                ApplicationErrorType.Validation);
        }

        var group = await FindActiveGroupAsync(groupId, cancellationToken);
        if (group is null)
        {
            return NotFound<GroupMemberResponse>();
        }

        var actor = await dbContext.GroupMembers.SingleOrDefaultAsync(
            member => member.GroupId == groupId && member.UserId == actorUserId,
            cancellationToken);
        var target = await dbContext.GroupMembers.SingleOrDefaultAsync(
            member => member.GroupId == groupId && member.UserId == targetUserId,
            cancellationToken);
        if (actor?.Role != GroupMemberRole.Owner || target is null)
        {
            return Forbidden<GroupMemberResponse>();
        }

        if (target.Role == GroupMemberRole.Owner && targetUserId == actorUserId &&
            targetRole != GroupMemberRole.Owner)
        {
            return Failure<GroupMemberResponse>(
                "owner_cannot_demote",
                "Transfer ownership before changing the current owner's role.",
                ApplicationErrorType.Conflict);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (targetRole == GroupMemberRole.Owner)
        {
            if (targetUserId == actorUserId)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ApplicationResult<GroupMemberResponse>.Success(ToResponse(target));
            }

            actor.ChangeRole(GroupMemberRole.Admin);
            target.ChangeRole(GroupMemberRole.Owner);
            group.TransferOwnership(targetUserId, timeProvider.GetUtcNow());
        }
        else
        {
            target.ChangeRole(targetRole);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ApplicationResult<GroupMemberResponse>.Success(ToResponse(target));
    }

    public async Task<ApplicationResult> RemoveMemberAsync(
        Guid actorUserId,
        Guid groupId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetMemberRoleAsync(groupId, actorUserId, cancellationToken);
        var target = await dbContext.GroupMembers.SingleOrDefaultAsync(
            member => member.GroupId == groupId && member.UserId == targetUserId,
            cancellationToken);
        if (target is null || await FindActiveGroupAsync(groupId, cancellationToken) is null)
        {
            return NotFound();
        }

        if (target.UserId == actorUserId)
        {
            return Failure(
                "use_group_leave",
                "Use the leave endpoint to remove yourself from a group.",
                ApplicationErrorType.Validation);
        }

        var allowed = actor == GroupMemberRole.Owner
            ? target.Role != GroupMemberRole.Owner
            : actor == GroupMemberRole.Admin && target.Role == GroupMemberRole.Member;
        if (!allowed)
        {
            return Forbidden();
        }

        dbContext.GroupMembers.Remove(target);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult<IReadOnlyList<GroupRuleResponse>>> GetRulesAsync(
        Guid groupId,
        Guid? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await groupPostAccessService.CanViewGroupAsync(groupId, viewerUserId, cancellationToken))
        {
            return NotFound<IReadOnlyList<GroupRuleResponse>>();
        }

        var rules = await dbContext.GroupRules.AsNoTracking()
            .Where(rule => rule.GroupId == groupId)
            .OrderBy(rule => rule.SortOrder)
            .ThenBy(rule => rule.Id)
            .Select(rule => ToResponse(rule))
            .ToListAsync(cancellationToken);
        return ApplicationResult<IReadOnlyList<GroupRuleResponse>>.Success(rules);
    }

    public async Task<ApplicationResult<GroupRuleResponse>> CreateRuleAsync(
        Guid actorUserId,
        Guid groupId,
        CreateGroupRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageRulesAsync(actorUserId, groupId, cancellationToken))
        {
            return Forbidden<GroupRuleResponse>();
        }

        try
        {
            var rule = GroupRule.Create(
                Guid.NewGuid(),
                groupId,
                request.Title,
                request.Description,
                request.SortOrder);
            dbContext.GroupRules.Add(rule);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ApplicationResult<GroupRuleResponse>.Success(ToResponse(rule));
        }
        catch (ArgumentException exception)
        {
            return Failure<GroupRuleResponse>("invalid_group_rule", exception.Message, ApplicationErrorType.Validation);
        }
    }

    public async Task<ApplicationResult<GroupRuleResponse>> UpdateRuleAsync(
        Guid actorUserId,
        Guid groupId,
        Guid ruleId,
        UpdateGroupRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageRulesAsync(actorUserId, groupId, cancellationToken))
        {
            return Forbidden<GroupRuleResponse>();
        }

        var rule = await dbContext.GroupRules.SingleOrDefaultAsync(
            item => item.Id == ruleId && item.GroupId == groupId,
            cancellationToken);
        if (rule is null)
        {
            return NotFound<GroupRuleResponse>();
        }

        try
        {
            rule.Update(request.Title, request.Description, request.SortOrder);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ApplicationResult<GroupRuleResponse>.Success(ToResponse(rule));
        }
        catch (ArgumentException exception)
        {
            return Failure<GroupRuleResponse>("invalid_group_rule", exception.Message, ApplicationErrorType.Validation);
        }
    }

    public async Task<ApplicationResult> DeleteRuleAsync(
        Guid actorUserId,
        Guid groupId,
        Guid ruleId,
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageRulesAsync(actorUserId, groupId, cancellationToken))
        {
            return Forbidden();
        }

        var rule = await dbContext.GroupRules.SingleOrDefaultAsync(
            item => item.Id == ruleId && item.GroupId == groupId,
            cancellationToken);
        if (rule is null)
        {
            return NotFound();
        }

        dbContext.GroupRules.Remove(rule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult<PostResponse>> CreatePostAsync(
        Guid actorUserId,
        Guid groupId,
        CreatePostRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await FindActiveGroupAsync(groupId, cancellationToken) is null)
        {
            return NotFound<PostResponse>();
        }

        return await postsUseCase.CreateGroupPostAsync(
            actorUserId,
            groupId,
            request.Content,
            request.MediaIds ?? [],
            cancellationToken,
            request.TextBackground);
    }

    public async Task<ApplicationResult<GroupCursorPageResponse<PostResponse>>> GetPostsAsync(
        Guid groupId,
        Guid? viewerUserId,
        string? cursorValue,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var group = await FindActiveGroupAsync(groupId, cancellationToken);
        if (group is null ||
            !await groupPostAccessService.CanViewGroupAsync(group, viewerUserId, cancellationToken))
        {
            return NotFound<GroupCursorPageResponse<PostResponse>>();
        }

        if (!IsValidCursor(cursorValue) || !IsValidLimit(limit))
        {
            return InvalidPage<GroupCursorPageResponse<PostResponse>>();
        }

        var cursor = DecodeCursorOrNull(cursorValue);
        IReadOnlySet<Guid> blockedUserIds = viewerUserId is null
            ? new HashSet<Guid>()
            : (await friendsService.GetAccessSnapshotAsync(viewerUserId.Value, cancellationToken)).BlockedUserIds;
        var query = dbContext.Posts.AsNoTracking().Where(post =>
            post.DeletedAtUtc == null &&
            post.ContainerType == PostContainerType.Group &&
            post.ContainerId == groupId &&
            !blockedUserIds.Contains(post.AuthorUserId));
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
        var page = candidates.Take(limit).ToList();
        var items = await postsService.LoadResponsesAsync(page, viewerUserId, cancellationToken);
        return ApplicationResult<GroupCursorPageResponse<PostResponse>>.Success(
            new GroupCursorPageResponse<PostResponse>(
                items,
                candidates.Count > limit ? EncodeCursor(page[^1]) : null));
    }

    public async Task<ApplicationResult> RemovePostAsync(
        Guid actorUserId,
        Guid groupId,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        if (!await CanModerateAsync(actorUserId, groupId, cancellationToken))
        {
            return Forbidden();
        }

        var post = await dbContext.Posts.AsNoTracking().SingleOrDefaultAsync(
            item =>
                item.Id == postId &&
                item.DeletedAtUtc == null &&
                item.ContainerType == PostContainerType.Group &&
                item.ContainerId == groupId,
            cancellationToken);
        if (post is null)
        {
            return NotFound();
        }

        var deleted = await postsUseCase.DeletePostForModerationAsync(postId, cancellationToken);
        return deleted;
    }

    public async Task<ApplicationResult<Guid>> GetCoverMediaIdAsync(
        Guid groupId,
        Guid? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var group = await FindActiveGroupAsync(groupId, cancellationToken);
        if (group?.CoverMediaId is null ||
            !await groupPostAccessService.CanViewGroupAsync(group, viewerUserId, cancellationToken))
        {
            return NotFound<Guid>();
        }

        return ApplicationResult<Guid>.Success(group.CoverMediaId.Value);
    }

    private async Task<ApplicationResult<GroupInviteResponse>> RespondToInviteAsync(
        Guid actorUserId,
        Guid groupId,
        Guid inviteId,
        bool accept,
        CancellationToken cancellationToken)
    {
        var invite = await dbContext.GroupInvites.SingleOrDefaultAsync(
            item =>
                item.Id == inviteId &&
                item.GroupId == groupId &&
                item.InviteeUserId == actorUserId &&
                item.Status == GroupInviteStatus.Pending,
            cancellationToken);
        if (invite is null || await FindActiveGroupAsync(groupId, cancellationToken) is null)
        {
            return NotFound<GroupInviteResponse>();
        }

        if (accept && await dbContext.GroupMembers.AnyAsync(
                member => member.GroupId == groupId && member.UserId == actorUserId,
                cancellationToken))
        {
            return Failure<GroupInviteResponse>(
                "already_group_member",
                "The invitee is already a group member.",
                ApplicationErrorType.Conflict);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (accept)
        {
            invite.Accept(now);
            dbContext.GroupMembers.Add(GroupMember.Create(
                groupId,
                actorUserId,
                GroupMemberRole.Member,
                now));
        }
        else
        {
            invite.Decline(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ApplicationResult<GroupInviteResponse>.Success(ToResponse(invite));
    }

    private async Task<bool> CanModerateAsync(
        Guid actorUserId,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        if (await FindActiveGroupAsync(groupId, cancellationToken) is null)
        {
            return false;
        }

        return await GetMemberRoleAsync(groupId, actorUserId, cancellationToken) is GroupMemberRole.Owner
            or GroupMemberRole.Admin
            or GroupMemberRole.Moderator;
    }

    private async Task<bool> CanManageRulesAsync(
        Guid actorUserId,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var role = await GetMemberRoleAsync(groupId, actorUserId, cancellationToken);
        return role is GroupMemberRole.Owner or GroupMemberRole.Admin &&
            await FindActiveGroupAsync(groupId, cancellationToken) is not null;
    }

    private static bool CanManageGroup(GroupMemberRole? role) =>
        role is GroupMemberRole.Owner or GroupMemberRole.Admin;

    private Task<Group?> FindActiveGroupAsync(Guid groupId, CancellationToken cancellationToken) =>
        dbContext.Groups.SingleOrDefaultAsync(
            group => group.Id == groupId && group.DeletedAtUtc == null,
            cancellationToken);

    private Task<GroupMemberRole?> GetMemberRoleAsync(
        Guid groupId,
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.GroupMembers.AsNoTracking()
            .Where(member => member.GroupId == groupId && member.UserId == userId)
            .Select(member => (GroupMemberRole?)member.Role)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<Dictionary<Guid, int>> LoadMemberCountsAsync(
        IEnumerable<Guid> groupIds,
        CancellationToken cancellationToken)
    {
        var ids = groupIds.Distinct().ToArray();
        return ids.Length == 0
            ? []
            : await dbContext.GroupMembers.AsNoTracking()
                .Where(member => ids.Contains(member.GroupId))
                .GroupBy(member => member.GroupId)
                .Select(group => new { GroupId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.GroupId, item => item.Count, cancellationToken);
    }

    private static bool TryParsePrivacy(string value, out GroupPrivacy privacy) =>
        Enum.TryParse(value, true, out privacy) && Enum.IsDefined(privacy);

    private static bool TryParseRole(string value, out GroupMemberRole role) =>
        Enum.TryParse(value, true, out role) && Enum.IsDefined(role);

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

    private static bool IsValidLimit(int limit) => limit is >= 1 and <= MaximumPageSize;

    private static GroupCursor? DecodeCursorOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : DecodeCursor(value);

    private static GroupCursor DecodeCursor(string value)
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
                throw new FormatException("The group cursor is invalid.");
            }

            return new GroupCursor(new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc)), id);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException("The group cursor is invalid.", exception);
        }
    }

    private static string EncodeCursor(Group group) => EncodeCursor(group.CreatedAtUtc, group.Id);

    private static string EncodeCursor(Post post) => EncodeCursor(post.CreatedAtUtc, post.Id);

    private static string EncodeCursor(DateTimeOffset createdAtUtc, Guid id)
    {
        var payload = createdAtUtc.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture) +
            ":" + id.ToString("N");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static GroupResponse ToResponse(
        Group group,
        int memberCount,
        GroupMemberRole? viewerRole) =>
        new(
            group.Id,
            group.Name,
            group.Description,
            group.Privacy.ToString().ToLowerInvariant(),
            group.OwnerUserId,
            group.CoverMediaId is null ? null : $"/api/groups/{group.Id}/cover",
            memberCount,
            viewerRole?.ToString().ToLowerInvariant(),
            group.CreatedAtUtc,
            group.UpdatedAtUtc);

    private sealed record GroupMemberProfile(Guid UserId, string Username, string DisplayName, string? AvatarUrl);

    private static GroupMemberResponse ToResponse(GroupMember member, GroupMemberProfile? profile = null) =>
        new(member.UserId, member.Role.ToString().ToLowerInvariant(), member.JoinedAtUtc,
            profile?.Username, profile?.DisplayName, profile?.AvatarUrl);

    private static GroupJoinRequestResponse ToResponse(GroupJoinRequest request) =>
        new(
            request.Id,
            request.RequesterUserId,
            request.Status.ToString().ToLowerInvariant(),
            request.CreatedAtUtc,
            request.RespondedAtUtc,
            request.RespondedByUserId);

    private static GroupInviteResponse ToResponse(GroupInvite invite, GroupResponse? group = null) =>
        new(
            invite.Id,
            invite.GroupId,
            invite.InviterUserId,
            invite.InviteeUserId,
            invite.Status.ToString().ToLowerInvariant(),
            invite.CreatedAtUtc,
            invite.RespondedAtUtc,
            group);

    private static GroupRuleResponse ToResponse(GroupRule rule) =>
        new(rule.Id, rule.GroupId, rule.Title, rule.Description, rule.SortOrder);

    private static ApplicationResult<T> InvalidPage<T>() =>
        Failure<T>(
            "invalid_group_cursor",
            "The group cursor or limit is invalid.",
            ApplicationErrorType.Validation);

    private static ApplicationResult<T> NotFound<T>() =>
        Failure<T>("group_not_found", "The group was not found.", ApplicationErrorType.NotFound);

    private static ApplicationResult NotFound() =>
        ApplicationResult.Failure(
            new ApplicationError("group_not_found", "The group was not found.", ApplicationErrorType.NotFound));

    private static ApplicationResult<T> Forbidden<T>() =>
        Failure<T>("group_forbidden", "You are not allowed to perform this group action.", ApplicationErrorType.Forbidden);

    private static ApplicationResult Forbidden() =>
        ApplicationResult.Failure(
            new ApplicationError(
                "group_forbidden",
                "You are not allowed to perform this group action.",
                ApplicationErrorType.Forbidden));

    private static ApplicationResult<T> Failure<T>(
        string code,
        string message,
        ApplicationErrorType type) =>
        ApplicationResult<T>.Failure(new ApplicationError(code, message, type));

    private static ApplicationResult Failure(
        string code,
        string message,
        ApplicationErrorType type) =>
        ApplicationResult.Failure(new ApplicationError(code, message, type));

    private sealed record GroupCursor(DateTimeOffset CreatedAtUtc, Guid Id);
}
