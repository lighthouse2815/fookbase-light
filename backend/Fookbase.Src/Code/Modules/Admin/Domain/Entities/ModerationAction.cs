using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Admin.Domain.Enums;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Admin.Entities;

[Index(nameof(TargetType), nameof(TargetId), nameof(CreatedAtUtc))]
[Index(nameof(SubjectUserId), nameof(CreatedAtUtc))]
public sealed class ModerationAction
{
    private ModerationAction()
    {
    }

    public ModerationAction(
        Guid? reportId,
        Guid moderatorUserId,
        Guid subjectUserId,
        ReportTargetType targetType,
        Guid targetId,
        ModerationActionType actionType,
        string reason,
        string? internalNote,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? expiresAtUtc = null)
    {
        Id = Guid.NewGuid();
        ModeratorUserId = moderatorUserId;
        SubjectUserId = subjectUserId;
        ReportId = reportId;
        TargetType = targetType;
        TargetId = targetId;
        ActionType = actionType;
        Reason = reason.Trim();
        InternalNote = TextNormalization.NormalizeOptionalText(internalNote);
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    [Key]
    public Guid Id { get; private set; }

    public Guid ModeratorUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User ModeratorUser { get; private set; } = null!;

    public Guid SubjectUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User SubjectUser { get; private set; } = null!;

    public Guid? ReportId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public ContentReport? Report { get; private set; }

    public ReportTargetType TargetType { get; private set; }

    public Guid TargetId { get; private set; }

    public ModerationActionType ActionType { get; private set; }

    [Required]
    [MaxLength(500)]
    public string Reason { get; private set; } = string.Empty;

    [MaxLength(2_000)]
    public string? InternalNote { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ExpiresAtUtc { get; private set; }
}
