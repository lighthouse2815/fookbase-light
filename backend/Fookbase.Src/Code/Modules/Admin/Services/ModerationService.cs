using Fookbase.Api.Modules.Admin.Domain.Enums;
using Fookbase.Api.Modules.Notifications.Domain.Enums;
using Fookbase.Api.Modules.Admin.Common;
using Fookbase.Api.Modules.Admin.DTOs.Responses;
using Fookbase.Api.Modules.Admin.Entities;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Notifications.Entities;
using Fookbase.Api.Modules.Notifications.Services;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Posts.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Admin.Services;

public sealed class ModerationService(
    FookbaseDbContext dbContext,
    UserManager<User> userManager,
    PostsService postsService,
    NotificationService notificationService,
    TimeProvider timeProvider)
{
    public const int MaximumPageSize = 100;

    public async Task<ApplicationResult<ReportDetailResponse>> GetReportAsync(Guid reportId, CancellationToken cancellationToken = default)
    {
        var report = await dbContext.ContentReports.AsNoTracking().SingleOrDefaultAsync(item => item.Id == reportId, cancellationToken);
        if (report is null) return ApplicationResult<ReportDetailResponse>.Failure(NotFound("The report was not found."));
        var subjectUserId = await GetSubjectUserIdAsync(report, cancellationToken);
        var actions = await dbContext.ModerationActions.AsNoTracking()
            .Where(action => action.ReportId == reportId || (subjectUserId != null && action.SubjectUserId == subjectUserId))
            .OrderByDescending(action => action.CreatedAtUtc).ThenByDescending(action => action.Id).Take(20)
            .Select(action => ToResponse(action)).ToListAsync(cancellationToken);
        var count = await dbContext.ContentReports.CountAsync(item => item.TargetType == report.TargetType && item.TargetId == report.TargetId, cancellationToken);
        return ApplicationResult<ReportDetailResponse>.Success(new ReportDetailResponse(report.Id, report.ReporterUserId,
            report.TargetType.ToString().ToLowerInvariant(), report.TargetId, report.Reason.ToApiName().ToLowerInvariant(), report.Details,
            report.Status.ToString().ToLowerInvariant(), report.CreatedAtUtc, report.ResolvedAtUtc,
            await GetTargetPreviewAsync(report, cancellationToken), subjectUserId, actions, count));
    }

    public async Task<ApplicationResult<ModerationQueuePageResponse>> GetQueueAsync(string? status, string? targetType,
        string? cursorValue, int limit, CancellationToken cancellationToken = default)
    {
        var cursor = ModerationCursor.DecodeOrNull(cursorValue);
        ContentReportStatus? parsedStatus = string.IsNullOrWhiteSpace(status) ? null : Enum.Parse<ContentReportStatus>(status, true);
        ReportTargetType? parsedTarget = string.IsNullOrWhiteSpace(targetType) ? null : Enum.Parse<ReportTargetType>(targetType, true);
        var query = dbContext.ContentReports.AsNoTracking();
        if (parsedStatus is not null) query = query.Where(report => report.Status == parsedStatus);
        if (parsedTarget is not null) query = query.Where(report => report.TargetType == parsedTarget);
        if (cursor is not null) query = query.Where(report => report.CreatedAtUtc > cursor.CreatedAtUtc || (report.CreatedAtUtc == cursor.CreatedAtUtc && report.Id.CompareTo(cursor.Id) > 0));
        var reports = await query.OrderBy(report => report.CreatedAtUtc).ThenBy(report => report.Id).Take(limit + 1).ToListAsync(cancellationToken);
        var page = reports.Take(limit).ToArray();
        var reportCounts = page.Length == 0 ? new Dictionary<(ReportTargetType, Guid), int>() : await dbContext.ContentReports.AsNoTracking()
            .Where(item => page.Select(report => report.TargetId).Contains(item.TargetId))
            .GroupBy(item => new { item.TargetType, item.TargetId }).Select(group => new { group.Key.TargetType, group.Key.TargetId, Count = group.Count() })
            .ToDictionaryAsync(item => (item.TargetType, item.TargetId), item => item.Count, cancellationToken);
        var userIds = page.Where(report => report.TargetType == ReportTargetType.USER).Select(report => report.TargetId).ToArray();
        var postIds = page.Where(report => report.TargetType == ReportTargetType.POST).Select(report => report.TargetId).ToArray();
        var users = userIds.Length == 0 ? new Dictionary<Guid, string?>() : await dbContext.Users.AsNoTracking().Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, user.UserName }).ToDictionaryAsync(user => user.Id, user => user.UserName, cancellationToken);
        var posts = postIds.Length == 0 ? new Dictionary<Guid, (Guid AuthorUserId, string Content)>() : await dbContext.Posts.AsNoTracking().Where(post => postIds.Contains(post.Id))
            .Select(post => new { post.Id, post.AuthorUserId, post.Content }).ToDictionaryAsync(post => post.Id, post => (post.AuthorUserId, post.Content), cancellationToken);
        var items = new List<ModerationQueueReportResponse>(page.Length);
        foreach (var report in page)
        {
            Guid? subjectUserId = report.TargetType == ReportTargetType.USER
                ? (users.ContainsKey(report.TargetId) ? (Guid?)report.TargetId : null)
                : (posts.GetValueOrDefault(report.TargetId).AuthorUserId == Guid.Empty ? null : (Guid?)posts[report.TargetId].AuthorUserId);
            var preview = report.TargetType == ReportTargetType.USER ? users.GetValueOrDefault(report.TargetId) : posts.GetValueOrDefault(report.TargetId).Content;
            if (preview is { Length: > 160 }) preview = preview[..160];
            items.Add(new(report.Id, report.TargetType.ToString().ToLowerInvariant(), report.TargetId, report.Reason.ToApiName().ToLowerInvariant(), report.Details,
                report.Status.ToString().ToLowerInvariant(), report.CreatedAtUtc, preview, subjectUserId,
                reportCounts.GetValueOrDefault((report.TargetType, report.TargetId))));
        }
        return ApplicationResult<ModerationQueuePageResponse>.Success(new(items, reports.Count > limit ? new ModerationCursor(page[^1].CreatedAtUtc, page[^1].Id).Encode() : null));
    }

    public async Task<ApplicationResult<ModerationActionResponse>> DismissReportAsync(Guid moderatorUserId, Guid reportId,
        string? reason, string? internalNote, CancellationToken cancellationToken = default)
    {
        var report = await dbContext.ContentReports.SingleOrDefaultAsync(item => item.Id == reportId, cancellationToken);
        if (report is null) return ApplicationResult<ModerationActionResponse>.Failure(NotFound("The report was not found."));
        var existing = await dbContext.ModerationActions.AsNoTracking().Where(action => action.ReportId == reportId && action.ActionType == ModerationActionType.DISMISS_REPORT)
            .OrderByDescending(action => action.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null) return ApplicationResult<ModerationActionResponse>.Success(ToResponse(existing));
        var subjectUserId = await GetSubjectUserIdAsync(report, cancellationToken);
        if (subjectUserId is null) return ApplicationResult<ModerationActionResponse>.Failure(NotFound("The reported target was not found."));
        var now = timeProvider.GetUtcNow();
        report.UpdateStatus(ContentReportStatus.DISMISSED, now);
        var action = new ModerationAction(report.Id, moderatorUserId, subjectUserId.Value, report.TargetType, report.TargetId,
            ModerationActionType.DISMISS_REPORT, reason ?? "No violation found.", internalNote, now);
        dbContext.ModerationActions.Add(action);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult<ModerationActionResponse>.Success(ToResponse(action));
    }

    public async Task<ApplicationResult<ModerationActionResponse>> RemoveReportedContentAsync(Guid moderatorUserId, Guid reportId,
        string? reason, string? internalNote, CancellationToken cancellationToken = default)
    {
        var report = await dbContext.ContentReports.SingleOrDefaultAsync(item => item.Id == reportId, cancellationToken);
        if (report is null) return ApplicationResult<ModerationActionResponse>.Failure(NotFound("The report was not found."));
        if (report.TargetType != ReportTargetType.POST) return ApplicationResult<ModerationActionResponse>.Failure(Validation("Only reported posts can be removed in Moderation V1."));
        var post = await dbContext.Posts.SingleOrDefaultAsync(item => item.Id == report.TargetId, cancellationToken);
        if (post is null) return ApplicationResult<ModerationActionResponse>.Failure(NotFound("The reported post was not found."));
        var now = timeProvider.GetUtcNow();
        if (post.DeletedAtUtc is null)
        {
            var removed = await postsService.DeletePostForModerationAsync(post.Id, cancellationToken);
            if (!removed.Succeeded) return ApplicationResult<ModerationActionResponse>.Failure(Validation(
                "The reported post could not be removed."));
        }
        report.UpdateStatus(ContentReportStatus.REVIEWED, now);
        var action = new ModerationAction(report.Id, moderatorUserId, post.AuthorUserId, report.TargetType, report.TargetId,
            ModerationActionType.REMOVE_POST, reason ?? "Content violated community rules.", internalNote, now);
        dbContext.ModerationActions.Add(action);
        var notification = await notificationService.QueueAsync(post.AuthorUserId, null, NotificationType.ACCOUNT_WARNING, null, null, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (notification is not null) await notificationService.PublishAsync(notification, cancellationToken);
        return ApplicationResult<ModerationActionResponse>.Success(ToResponse(action));
    }

    public async Task<ApplicationResult<ModerationActionResponse>> WarnReportedUserAsync(Guid moderatorUserId, Guid reportId,
        string? reason, string? internalNote, CancellationToken cancellationToken = default)
    {
        var report = await dbContext.ContentReports.SingleOrDefaultAsync(item => item.Id == reportId, cancellationToken);
        if (report is null) return ApplicationResult<ModerationActionResponse>.Failure(NotFound("The report was not found."));
        var subjectUserId = await GetSubjectUserIdAsync(report, cancellationToken);
        return subjectUserId is null ? ApplicationResult<ModerationActionResponse>.Failure(NotFound("The reported target was not found.")) :
            await WarnUserAsync(moderatorUserId, subjectUserId.Value, report, reason, internalNote, cancellationToken);
    }

    public async Task<ApplicationResult<ModerationActionResponse>> WarnUserAsync(Guid moderatorUserId, Guid userId,
        string? reason, string? internalNote, CancellationToken cancellationToken = default) =>
        await WarnUserAsync(moderatorUserId, userId, null, reason, internalNote, cancellationToken);

    public async Task<ApplicationResult<ModerationActionResponse>> SuspendUserAsync(Guid moderatorUserId, Guid userId,
        string? reason, int? durationHours, DateTimeOffset? suspendedUntilUtc, string? internalNote, CancellationToken cancellationToken = default)
    {
        if (moderatorUserId == userId) return ApplicationResult<ModerationActionResponse>.Failure(Forbidden("Administrators cannot suspend themselves."));
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return ApplicationResult<ModerationActionResponse>.Failure(NotFound("The user was not found."));
        var now = timeProvider.GetUtcNow();
        var until = suspendedUntilUtc ?? now.AddHours(durationHours!.Value);
        var state = await GetOrCreateStateAsync(userId, now, cancellationToken);
        state.Suspend(until, now);
        var action = new ModerationAction(null, moderatorUserId, userId, ReportTargetType.USER, userId,
            ModerationActionType.SUSPEND_USER, reason ?? "Account temporarily suspended.", internalNote, now, until);
        dbContext.ModerationActions.Add(action);
        await RevokeAllSessionsAsync(userId, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult<ModerationActionResponse>.Success(ToResponse(action));
    }

    public async Task<ApplicationResult<ModerationActionResponse>> DisableUserAsync(Guid moderatorUserId, Guid userId,
        string? reason, string? internalNote, CancellationToken cancellationToken = default)
    {
        if (moderatorUserId == userId) return ApplicationResult<ModerationActionResponse>.Failure(Forbidden("Administrators cannot disable themselves."));
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return ApplicationResult<ModerationActionResponse>.Failure(NotFound("The user was not found."));
        var now = timeProvider.GetUtcNow();
        var state = await GetOrCreateStateAsync(userId, now, cancellationToken);
        state.Disable(now);
        var action = new ModerationAction(null, moderatorUserId, userId, ReportTargetType.USER, userId,
            ModerationActionType.DISABLE_USER, reason ?? "Account disabled.", internalNote, now);
        dbContext.ModerationActions.Add(action);
        await RevokeAllSessionsAsync(userId, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult<ModerationActionResponse>.Success(ToResponse(action));
    }

    public async Task<ApplicationResult<ModerationActionResponse>> UnsuspendUserAsync(Guid moderatorUserId, Guid userId,
        string? reason, string? internalNote, CancellationToken cancellationToken = default) =>
        await ReverseAccountActionAsync(moderatorUserId, userId, false, reason, internalNote, cancellationToken);

    public async Task<ApplicationResult<ModerationActionResponse>> EnableUserAsync(Guid moderatorUserId, Guid userId,
        string? reason, string? internalNote, CancellationToken cancellationToken = default) =>
        await ReverseAccountActionAsync(moderatorUserId, userId, true, reason, internalNote, cancellationToken);

    public async Task<ApplicationResult<UserModerationStateResponse>> GetStateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var state = await dbContext.UserModerationStates.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return ApplicationResult<UserModerationStateResponse>.Success(ToResponse(state ?? new UserModerationState(userId, timeProvider.GetUtcNow())));
    }

    public async Task<ApplicationResult<ModerationActionPageResponse>> GetHistoryAsync(Guid userId, string? cursorValue, int limit, CancellationToken cancellationToken = default)
    {
        var cursor = ModerationCursor.DecodeOrNull(cursorValue);
        var query = dbContext.ModerationActions.AsNoTracking().Where(action => action.SubjectUserId == userId);
        if (cursor is not null) query = query.Where(action => action.CreatedAtUtc < cursor.CreatedAtUtc || (action.CreatedAtUtc == cursor.CreatedAtUtc && action.Id.CompareTo(cursor.Id) < 0));
        var candidates = await query.OrderByDescending(action => action.CreatedAtUtc).ThenByDescending(action => action.Id).Take(limit + 1).ToListAsync(cancellationToken);
        var items = candidates.Take(limit).Select(ToResponse).ToArray();
        return ApplicationResult<ModerationActionPageResponse>.Success(new(items, candidates.Count > limit ? new ModerationCursor(candidates[limit - 1].CreatedAtUtc, candidates[limit - 1].Id).Encode() : null));
    }

    private async Task<ApplicationResult<ModerationActionResponse>> WarnUserAsync(Guid moderatorUserId, Guid userId, ContentReport? report,
        string? reason, string? internalNote, CancellationToken cancellationToken)
    {
        if (await userManager.FindByIdAsync(userId.ToString()) is null) return ApplicationResult<ModerationActionResponse>.Failure(NotFound("The user was not found."));
        var now = timeProvider.GetUtcNow();
        var state = await GetOrCreateStateAsync(userId, now, cancellationToken);
        state.Warn(now);
        report?.UpdateStatus(ContentReportStatus.REVIEWED, now);
        var targetType = report?.TargetType ?? ReportTargetType.USER;
        var targetId = report?.TargetId ?? userId;
        var action = new ModerationAction(report?.Id, moderatorUserId, userId, targetType, targetId,
            ModerationActionType.WARN_USER, reason ?? "Account warning.", internalNote, now);
        dbContext.ModerationActions.Add(action);
        var notification = await notificationService.QueueAsync(userId, null, NotificationType.ACCOUNT_WARNING, null, null, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (notification is not null) await notificationService.PublishAsync(notification, cancellationToken);
        return ApplicationResult<ModerationActionResponse>.Success(ToResponse(action));
    }

    private async Task<ApplicationResult<ModerationActionResponse>> ReverseAccountActionAsync(Guid moderatorUserId, Guid userId, bool enable,
        string? reason, string? internalNote, CancellationToken cancellationToken)
    {
        if (moderatorUserId == userId) return ApplicationResult<ModerationActionResponse>.Failure(Forbidden("Administrators cannot change their own moderation state."));
        if (await userManager.FindByIdAsync(userId.ToString()) is null) return ApplicationResult<ModerationActionResponse>.Failure(NotFound("The user was not found."));
        var now = timeProvider.GetUtcNow();
        var state = await GetOrCreateStateAsync(userId, now, cancellationToken);
        if (enable) state.Enable(now); else state.Unsuspend(now);
        var action = new ModerationAction(null, moderatorUserId, userId, ReportTargetType.USER, userId,
            enable ? ModerationActionType.ENABLE_USER : ModerationActionType.UNSUSPEND_USER,
            reason ?? (enable ? "Account enabled." : "Account suspension cleared."), internalNote, now);
        dbContext.ModerationActions.Add(action);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult<ModerationActionResponse>.Success(ToResponse(action));
    }

    private async Task<UserModerationState> GetOrCreateStateAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var state = await dbContext.UserModerationStates.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (state is not null) return state;
        state = new UserModerationState(userId, now);
        dbContext.UserModerationStates.Add(state);
        return state;
    }

    private async Task<Guid?> GetSubjectUserIdAsync(ContentReport report, CancellationToken cancellationToken) => report.TargetType switch
    {
        ReportTargetType.USER => await dbContext.Users.AsNoTracking().Where(user => user.Id == report.TargetId).Select(user => (Guid?)user.Id).SingleOrDefaultAsync(cancellationToken),
        ReportTargetType.POST => await dbContext.Posts.AsNoTracking().Where(post => post.Id == report.TargetId).Select(post => (Guid?)post.AuthorUserId).SingleOrDefaultAsync(cancellationToken),
        _ => null
    };

    private async Task<string?> GetTargetPreviewAsync(ContentReport report, CancellationToken cancellationToken)
    {
        if (report.TargetType == ReportTargetType.USER)
            return await dbContext.Users.AsNoTracking().Where(user => user.Id == report.TargetId).Select(user => user.UserName).SingleOrDefaultAsync(cancellationToken);
        if (report.TargetType != ReportTargetType.POST) return null;
        var content = await dbContext.Posts.AsNoTracking().Where(post => post.Id == report.TargetId).Select(post => post.Content).SingleOrDefaultAsync(cancellationToken);
        return content is { Length: > 160 } ? content[..160] : content;
    }

    private async Task RevokeAllSessionsAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await dbContext.AuthSessions.Where(session => session.UserId == userId && session.RevokedAtUtc == null && session.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAtUtc, now), cancellationToken);
        await dbContext.RefreshTokens.Where(token => token.UserId == userId && token.RevokedAt == null && token.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);
    }

    private static ModerationActionResponse ToResponse(ModerationAction action) => new(action.Id, action.ReportId, action.SubjectUserId,
        action.TargetType.ToString().ToLowerInvariant(), action.TargetId, action.ActionType.ToApiName(), action.Reason, action.InternalNote, action.CreatedAtUtc, action.ExpiresAtUtc);
    private static UserModerationStateResponse ToResponse(UserModerationState state) => new(state.UserId, state.WarningCount, state.SuspendedUntilUtc, state.DisabledAtUtc, state.UpdatedAtUtc);
    private static ApplicationError Validation(string message) => new("moderation_validation_failed", message, ApplicationErrorType.VALIDATION);
    private static ApplicationError NotFound(string message) => new("moderation_target_not_found", message, ApplicationErrorType.NOT_FOUND);
    private static ApplicationError Forbidden(string message) => new("moderation_action_forbidden", message, ApplicationErrorType.FORBIDDEN);
}
