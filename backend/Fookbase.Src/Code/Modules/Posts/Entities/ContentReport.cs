namespace Fookbase.Api.Modules.Posts.Entities;

public enum ReportTargetType
{
    User,
    Post
}

public enum ReportReason
{
    Spam,
    Harassment,
    HateSpeech,
    Nudity,
    Violence,
    Scam,
    Other
}

public enum ContentReportStatus
{
    Pending,
    Reviewed,
    Resolved,
    Dismissed
}

public sealed class ContentReport
{
    public const int MaximumDetailsLength = 500;

    private ContentReport()
    {
    }

    private ContentReport(
        Guid id,
        Guid reporterUserId,
        ReportTargetType targetType,
        Guid targetId,
        ReportReason reason,
        string? details,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        ReporterUserId = reporterUserId;
        TargetType = targetType;
        TargetId = targetId;
        Reason = reason;
        Details = details;
        Status = ContentReportStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid ReporterUserId { get; private set; }

    public ReportTargetType TargetType { get; private set; }

    public Guid TargetId { get; private set; }

    public ReportReason Reason { get; private set; }

    public string? Details { get; private set; }

    public ContentReportStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public static ContentReport Create(
        Guid reporterUserId,
        ReportTargetType targetType,
        Guid targetId,
        ReportReason reason,
        string? details,
        DateTimeOffset createdAtUtc) =>
        new(Guid.NewGuid(), reporterUserId, targetType, targetId, reason, details, createdAtUtc);

    public void UpdateStatus(ContentReportStatus status, DateTimeOffset resolvedAtUtc)
    {
        if (status == ContentReportStatus.Pending)
        {
            throw new ArgumentException("A report cannot be moved back to pending.");
        }

        Status = status;
        ResolvedAtUtc ??= resolvedAtUtc;
    }
}
