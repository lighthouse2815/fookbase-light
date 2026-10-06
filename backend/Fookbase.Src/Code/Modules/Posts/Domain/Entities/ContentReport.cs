using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Entities;

[Index(nameof(Status), nameof(CreatedAtUtc), nameof(Id))]
[Index(nameof(ReporterUserId), nameof(TargetType), nameof(TargetId), IsUnique = true)]
[Index(nameof(TargetType), nameof(TargetId), nameof(Status), nameof(CreatedAtUtc))]
public sealed class ContentReport
{
    public const int MaximumDetailsLength = 500;

    private ContentReport() { }

    public ContentReport(
        Guid reporterUserId,
        ReportTargetType targetType,
        Guid targetId,
        ReportReason reason,
        string? details,
        DateTimeOffset createdAtUtc)
    {
        Id = Guid.NewGuid();
        ReporterUserId = reporterUserId;
        TargetType = targetType;
        TargetId = targetId;
        Reason = reason;
        Details = details;
        Status = ContentReportStatus.PENDING;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid ReporterUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User ReporterUser { get; private set; } = null!;

    public ReportTargetType TargetType { get; private set; }

    public Guid TargetId { get; private set; }

    public ReportReason Reason { get; private set; }

    [MaxLength(MaximumDetailsLength)]
    public string? Details { get; private set; }

    public ContentReportStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public void UpdateStatus(ContentReportStatus status, DateTimeOffset resolvedAtUtc)
    {
        if (status == ContentReportStatus.PENDING)
        {
            throw new ArgumentException("A report cannot be moved back to pending.");
        }

        Status = status;
        ResolvedAtUtc ??= resolvedAtUtc;
    }
}
