using Fookbase.Api.Modules.Friends.Common;
using Fookbase.Api.Modules.Friends.Domain.Enums;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Friends.Config;
using Fookbase.Api.Modules.Friends.DTOs.Responses;
using Fookbase.Api.Modules.Friends.Entities;
using Fookbase.Api.Modules.Pages.Domain.Enums;
using Fookbase.Api.Modules.Pages.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Friends.Services;

public sealed class FriendSuggestionService(
    FookbaseDbContext dbContext,
    IDataProtectionProvider protectionProvider,
    FriendSuggestionOptions options)
{
    public async Task<ApplicationResult<CursorPageResponse<FriendSuggestionResponse>>> GetSuggestionsAsync(
        Guid viewerUserId,
        string? cursorValue,
        int? requestedLimit,
        CancellationToken cancellationToken = default)
    {
        var limit = requestedLimit ?? options.DefaultPageSize;
        var cursor = FriendSuggestionCursor.DecodeOrNull(cursorValue, viewerUserId, protectionProvider);

        var viewerFriendIds = FriendIds(viewerUserId);
        var candidateIds = CandidateIds(viewerUserId, viewerFriendIds);
        var scored =
            from candidateUserId in candidateIds
            join profile in dbContext.UserProfiles.AsNoTracking() on candidateUserId equals profile.UserId
            join user in dbContext.Users.AsNoTracking() on candidateUserId equals user.Id
            where candidateUserId != viewerUserId && user.IsActive &&
                  !dbContext.Friendships.AsNoTracking().Any(friendship =>
                      (friendship.User1Id == viewerUserId && friendship.User2Id == candidateUserId) ||
                      (friendship.User2Id == viewerUserId && friendship.User1Id == candidateUserId)) &&
                  !dbContext.FriendRequests.AsNoTracking().Any(request =>
                      request.Status == FriendRequestStatus.PENDING &&
                      ((request.SenderUserId == viewerUserId && request.ReceiverUserId == candidateUserId) ||
                       (request.SenderUserId == candidateUserId && request.ReceiverUserId == viewerUserId))) &&
                  !dbContext.BlockedUsers.AsNoTracking().Any(block =>
                      (block.BlockerUserId == viewerUserId && block.BlockedAccountId == candidateUserId) ||
                      (block.BlockerUserId == candidateUserId && block.BlockedAccountId == viewerUserId))
            let mutualFriendCount = dbContext.Friendships.AsNoTracking().Count(candidateFriendship =>
                (candidateFriendship.User1Id == candidateUserId && viewerFriendIds.Contains(candidateFriendship.User2Id)) ||
                (candidateFriendship.User2Id == candidateUserId && viewerFriendIds.Contains(candidateFriendship.User1Id)))
            let sharedGroupCount = (
                from viewerMembership in dbContext.GroupMembers.AsNoTracking()
                join candidateMembership in dbContext.GroupMembers.AsNoTracking()
                    on viewerMembership.GroupId equals candidateMembership.GroupId
                join groupEntity in dbContext.Groups.AsNoTracking() on viewerMembership.GroupId equals groupEntity.Id
                where viewerMembership.UserId == viewerUserId &&
                      candidateMembership.UserId == candidateUserId &&
                      groupEntity.DeletedAtUtc == null
                select groupEntity.Id).Count()
            let sharedPageCount = (
                from viewerFollower in dbContext.PageFollowers.AsNoTracking()
                join candidateFollower in dbContext.PageFollowers.AsNoTracking()
                    on viewerFollower.PageId equals candidateFollower.PageId
                join page in dbContext.Pages.AsNoTracking() on viewerFollower.PageId equals page.Id
                where viewerFollower.UserId == viewerUserId &&
                      candidateFollower.UserId == candidateUserId &&
                      page.DeletedAtUtc == null && page.Status == PageStatus.PUBLISHED
                select page.Id).Count()
            let score = mutualFriendCount * options.MutualFriendWeight +
                        sharedGroupCount * options.SharedGroupWeight +
                        sharedPageCount * options.SharedPageWeight
            select new
            {
                UserId = candidateUserId,
                profile.Username,
                profile.DisplayName,
                profile.AvatarUrl,
                profile.AvatarMediaId,
                MutualFriendCount = mutualFriendCount,
                SharedGroupCount = sharedGroupCount,
                SharedPageCount = sharedPageCount,
                Score = score,
                IsFollowing = dbContext.UserFollows.AsNoTracking().Any(follow =>
                    follow.FollowerUserId == viewerUserId && follow.FollowingUserId == candidateUserId)
            };

        var total = await scored.CountAsync(cancellationToken);
        if (cursor is not null)
        {
            scored = scored.Where(item =>
                item.Score < cursor.Score ||
                (item.Score == cursor.Score && item.UserId.CompareTo(cursor.UserId) > 0));
        }

        var rows = await scored
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.UserId)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        var page = rows.Take(limit).ToList();
        var nextCursor = rows.Count > limit
            ? new FriendSuggestionCursor(FriendSuggestionCursor.CurrentVersion, viewerUserId, page[^1].Score, page[^1].UserId)
                .Encode(viewerUserId, protectionProvider)
            : null;
        var response = page.Select(item => new FriendSuggestionResponse(
            new FriendSuggestionProfileResponse(
                item.UserId,
                item.Username,
                item.DisplayName,
                item.AvatarMediaId is null ? item.AvatarUrl : $"/api/users/{item.UserId}/avatar"),
            item.MutualFriendCount,
            item.SharedGroupCount,
            item.SharedPageCount,
            "none",
            item.IsFollowing)).ToList();
        return ApplicationResult<CursorPageResponse<FriendSuggestionResponse>>.Success(
            new CursorPageResponse<FriendSuggestionResponse>(response, nextCursor, total));
    }

    private IQueryable<Guid> CandidateIds(Guid viewerUserId, IQueryable<Guid> viewerFriendIds)
    {
        var mutualFriendCandidates = dbContext.Friendships.AsNoTracking()
            .Where(friendship => viewerFriendIds.Contains(friendship.User1Id))
            .Select(friendship => friendship.User2Id)
            .Concat(dbContext.Friendships.AsNoTracking()
                .Where(friendship => viewerFriendIds.Contains(friendship.User2Id))
                .Select(friendship => friendship.User1Id));

        var sharedGroupCandidates =
            from viewerMembership in dbContext.GroupMembers.AsNoTracking()
            join candidateMembership in dbContext.GroupMembers.AsNoTracking()
                on viewerMembership.GroupId equals candidateMembership.GroupId
            join groupEntity in dbContext.Groups.AsNoTracking() on viewerMembership.GroupId equals groupEntity.Id
            where viewerMembership.UserId == viewerUserId &&
                  candidateMembership.UserId != viewerUserId &&
                  groupEntity.DeletedAtUtc == null
            select candidateMembership.UserId;

        var sharedPageCandidates =
            from viewerFollower in dbContext.PageFollowers.AsNoTracking()
            join candidateFollower in dbContext.PageFollowers.AsNoTracking()
                on viewerFollower.PageId equals candidateFollower.PageId
            join page in dbContext.Pages.AsNoTracking() on viewerFollower.PageId equals page.Id
            where viewerFollower.UserId == viewerUserId &&
                  candidateFollower.UserId != viewerUserId &&
                  page.DeletedAtUtc == null && page.Status == PageStatus.PUBLISHED
            select candidateFollower.UserId;

        return mutualFriendCandidates.Distinct().OrderBy(userId => userId).Take(options.CandidateLimitPerSource)
            .Concat(sharedGroupCandidates.Distinct().OrderBy(userId => userId).Take(options.CandidateLimitPerSource))
            .Concat(sharedPageCandidates.Distinct().OrderBy(userId => userId).Take(options.CandidateLimitPerSource))
            .Distinct();
    }

    private IQueryable<Guid> FriendIds(Guid viewerUserId) =>
        dbContext.Friendships.AsNoTracking()
            .Where(friendship => friendship.User1Id == viewerUserId || friendship.User2Id == viewerUserId)
            .Select(friendship => friendship.User1Id == viewerUserId
                ? friendship.User2Id
                : friendship.User1Id);
}
