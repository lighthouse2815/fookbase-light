using System.Security.Cryptography;
using System.Text.Json;
using Fookbase.Api.Modules.Friends.Common;
using Fookbase.Api.Modules.Friends.Config;
using Fookbase.Api.Modules.Friends.DTOs.Responses;
using Fookbase.Api.Modules.Friends.Entities;
using Fookbase.Api.Modules.Pages.Entities;
using Fookbase.Api.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Friends.Services;

public sealed class FriendSuggestionService(
    FookbaseDbContext dbContext,
    IDataProtectionProvider protectionProvider,
    FriendSuggestionOptions options)
{
    private const int CursorVersion = 1;

    public async Task<ApplicationResult<CursorPageResponse<FriendSuggestionResponse>>> GetSuggestionsAsync(
        Guid viewerUserId,
        string? cursorValue,
        int? requestedLimit,
        CancellationToken cancellationToken = default)
    {
        var limit = requestedLimit ?? options.DefaultPageSize;
        if (limit is < 1 or > 50 || limit > options.MaximumPageSize)
        {
            return ApplicationResult<CursorPageResponse<FriendSuggestionResponse>>.Failure(
                Validation("invalid_suggestion_limit", $"Limit must be between 1 and {options.MaximumPageSize}."));
        }

        FriendSuggestionCursor? cursor;
        try
        {
            cursor = DecodeCursor(cursorValue, viewerUserId);
        }
        catch (FormatException)
        {
            return ApplicationResult<CursorPageResponse<FriendSuggestionResponse>>.Failure(
                Validation("invalid_suggestion_cursor", "The friend suggestion cursor is invalid."));
        }

        var viewerFriendIds = FriendIds(viewerUserId);
        var candidateIds = CandidateIds(viewerUserId, viewerFriendIds);
        var scored =
            from candidateUserId in candidateIds
            join profile in dbContext.UserProfiles.AsNoTracking() on candidateUserId equals profile.UserId
            join user in dbContext.Users.AsNoTracking() on candidateUserId equals user.Id
            where candidateUserId != viewerUserId && user.IsActive &&
                  !dbContext.Friendships.AsNoTracking().Any(friendship =>
                      (friendship.UserId1 == viewerUserId && friendship.UserId2 == candidateUserId) ||
                      (friendship.UserId2 == viewerUserId && friendship.UserId1 == candidateUserId)) &&
                  !dbContext.FriendRequests.AsNoTracking().Any(request =>
                      request.Status == FriendRequestStatus.Pending &&
                      ((request.SenderUserId == viewerUserId && request.ReceiverUserId == candidateUserId) ||
                       (request.SenderUserId == candidateUserId && request.ReceiverUserId == viewerUserId))) &&
                  !dbContext.BlockedUsers.AsNoTracking().Any(block =>
                      (block.BlockerUserId == viewerUserId && block.BlockedUserId == candidateUserId) ||
                      (block.BlockerUserId == candidateUserId && block.BlockedUserId == viewerUserId))
            let mutualFriendCount = dbContext.Friendships.AsNoTracking().Count(candidateFriendship =>
                (candidateFriendship.UserId1 == candidateUserId && viewerFriendIds.Contains(candidateFriendship.UserId2)) ||
                (candidateFriendship.UserId2 == candidateUserId && viewerFriendIds.Contains(candidateFriendship.UserId1)))
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
                      page.DeletedAtUtc == null && page.Status == PageStatus.Published
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
            ? EncodeCursor(new FriendSuggestionCursor(CursorVersion, viewerUserId, page[^1].Score, page[^1].UserId), viewerUserId)
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
            .Where(friendship => viewerFriendIds.Contains(friendship.UserId1))
            .Select(friendship => friendship.UserId2)
            .Concat(dbContext.Friendships.AsNoTracking()
                .Where(friendship => viewerFriendIds.Contains(friendship.UserId2))
                .Select(friendship => friendship.UserId1));

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
                  page.DeletedAtUtc == null && page.Status == PageStatus.Published
            select candidateFollower.UserId;

        return mutualFriendCandidates.Distinct().OrderBy(userId => userId).Take(options.CandidateLimitPerSource)
            .Concat(sharedGroupCandidates.Distinct().OrderBy(userId => userId).Take(options.CandidateLimitPerSource))
            .Concat(sharedPageCandidates.Distinct().OrderBy(userId => userId).Take(options.CandidateLimitPerSource))
            .Distinct();
    }

    private IQueryable<Guid> FriendIds(Guid viewerUserId) =>
        dbContext.Friendships.AsNoTracking()
            .Where(friendship => friendship.UserId1 == viewerUserId || friendship.UserId2 == viewerUserId)
            .Select(friendship => friendship.UserId1 == viewerUserId
                ? friendship.UserId2
                : friendship.UserId1);

    private FriendSuggestionCursor? DecodeCursor(string? value, Guid viewerUserId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            if (value.Length is < 1 or > 4_096)
            {
                throw new FormatException();
            }

            var cursor = JsonSerializer.Deserialize<FriendSuggestionCursor>(
                CreateCursorProtector(viewerUserId).Unprotect(value)) ?? throw new FormatException();
            return cursor.Version == CursorVersion && cursor.ViewerUserId == viewerUserId
                ? cursor
                : throw new FormatException();
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or ArgumentException)
        {
            throw new FormatException("The friend suggestion cursor is invalid.", exception);
        }
    }

    private string EncodeCursor(FriendSuggestionCursor cursor, Guid viewerUserId) =>
        CreateCursorProtector(viewerUserId).Protect(JsonSerializer.Serialize(cursor));

    private IDataProtector CreateCursorProtector(Guid viewerUserId) =>
        protectionProvider.CreateProtector(
            "Fookbase.FriendSuggestions",
            CursorVersion.ToString(),
            viewerUserId.ToString("N"));

    private static ApplicationError Validation(string code, string message) =>
        new(code, message, ApplicationErrorType.Validation);

    private sealed record FriendSuggestionCursor(int Version, Guid ViewerUserId, int Score, Guid UserId);

}
