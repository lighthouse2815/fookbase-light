using Fookbase.Api.Modules.Posts.Common;
using Fookbase.Api.Modules.Posts.Data;
using Fookbase.Api.Modules.Posts.DTOs.Requests;
using Fookbase.Api.Modules.Posts.DTOs.Responses;
using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Services;

public sealed class ReportsService(PostsDbContext dbContext, TimeProvider timeProvider)
{
    public Task<ApplicationResult<ContentReportResponse>> ReportUserAsync(
        Guid reporterUserId,
        Guid reportedUserId,
        CreateReportRequest request,
        CancellationToken cancellationToken = default) =>
        CreateAsync(reporterUserId, ReportTargetType.User, reportedUserId, request, cancellationToken);

    public async Task<ApplicationResult<ContentReportResponse>> ReportPostAsync(
        Guid reporterUserId,
        Guid postId,
        CreateReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var post = await dbContext.Posts.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == postId && item.DeletedAtUtc == null,
            cancellationToken);
        if (post is null)
        {
            return ApplicationResult<ContentReportResponse>.Failure(NotFound("The post was not found."));
        }

        if (post.AuthorUserId == reporterUserId)
        {
            return ApplicationResult<ContentReportResponse>.Failure(Validation(
                "You cannot report your own account or content."));
        }

        return await CreateAsync(reporterUserId, ReportTargetType.Post, postId, request, cancellationToken);
    }

    private async Task<ApplicationResult<ContentReportResponse>> CreateAsync(
        Guid reporterUserId,
        ReportTargetType targetType,
        Guid targetId,
        CreateReportRequest request,
        CancellationToken cancellationToken)
    {
        if (reporterUserId == targetId)
        {
            return ApplicationResult<ContentReportResponse>.Failure(Validation(
                "You cannot report your own account or content."));
        }

        if (!TryParseReason(request.Reason, out var reason))
        {
            return ApplicationResult<ContentReportResponse>.Failure(Validation(
                "A valid report reason is required."));
        }

        var details = request.Details?.Trim();
        if (details?.Length > ContentReport.MaximumDetailsLength)
        {
            return ApplicationResult<ContentReportResponse>.Failure(Validation(
                $"Report details cannot exceed {ContentReport.MaximumDetailsLength} characters."));
        }

        var alreadyReported = await dbContext.ContentReports.AnyAsync(
            report => report.ReporterUserId == reporterUserId &&
                      report.TargetType == targetType && report.TargetId == targetId,
            cancellationToken);
        if (alreadyReported)
        {
            return ApplicationResult<ContentReportResponse>.Failure(new ApplicationError(
                "already_reported",
                "You have already reported this content.",
                ApplicationErrorType.Conflict));
        }

        var report = ContentReport.Create(
            reporterUserId,
            targetType,
            targetId,
            reason,
            string.IsNullOrWhiteSpace(details) ? null : details,
            timeProvider.GetUtcNow());
        dbContext.ContentReports.Add(report);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<ContentReportResponse>.Success(ToResponse(report));
    }

    private static bool TryParseReason(string? value, out ReportReason reason) =>
        Enum.TryParse(value, true, out reason) && Enum.IsDefined(reason);

    private static ContentReportResponse ToResponse(ContentReport report) =>
        new(
            report.Id,
            report.TargetType.ToString().ToLowerInvariant(),
            report.TargetId,
            report.Reason.ToString().ToLowerInvariant(),
            report.Details,
            report.Status.ToString().ToLowerInvariant(),
            report.CreatedAtUtc);

    private static ApplicationError Validation(string message) =>
        new("validation_failed", message, ApplicationErrorType.Validation);

    private static ApplicationError NotFound(string message) =>
        new("report_target_not_found", message, ApplicationErrorType.NotFound);
}
