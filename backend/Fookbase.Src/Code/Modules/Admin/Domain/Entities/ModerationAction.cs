using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Admin.Domain.Enums;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Admin.Entities;

[Table("ModerationActions")]
[Index(nameof(TargetType), nameof(TargetId), nameof(CreatedAtUtc))]
[Index(nameof(SubjectUserId), nameof(CreatedAtUtc))]
public sealed class ModerationAction
{
    public const int MaximumReasonLength = 500;
    public const int MaximumInternalNoteLength = 2_000;

    private ModerationAction() { }

    private ModerationAction(Guid? reportId, Guid moderatorUserId, Guid subjectUserId,
        ReportTargetType targetType, Guid targetId, ModerationActionType actionType,
        string reason, string? internalNote, DateTimeOffset createdAtUtc, DateTimeOffset? expiresAtUtc)
    {
        Id = Guid.NewGuid();
        ReportId = reportId;
        ModeratorUserId = moderatorUserId;
        SubjectUserId = subjectUserId;
        TargetType = targetType;
        TargetId = targetId;
        ActionType = actionType;
        Reason = reason;
        InternalNote = internalNote;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    [Key]
    public Guid Id { get; private set; }
    public Guid? ReportId { get; private set; }
    public Guid ModeratorUserId { get; private set; }
    public Guid SubjectUserId { get; private set; }
    public ReportTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public ModerationActionType ActionType { get; private set; }
    [Required]
    [MaxLength(MaximumReasonLength)]
    public string Reason { get; private set; } = string.Empty;

    [MaxLength(MaximumInternalNoteLength)]
    public string? InternalNote { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    [ForeignKey(nameof(ReportId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public ContentReport? Report { get; private set; }

    [ForeignKey(nameof(ModeratorUserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User ModeratorUser { get; private set; } = null!;

    [ForeignKey(nameof(SubjectUserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User SubjectUser { get; private set; } = null!;

    public static ModerationAction Create(Guid? reportId, Guid moderatorUserId, Guid subjectUserId,
        ReportTargetType targetType, Guid targetId, ModerationActionType actionType,
        string reason, string? internalNote, DateTimeOffset createdAtUtc, DateTimeOffset? expiresAtUtc = null) =>
        new(reportId, moderatorUserId, subjectUserId, targetType, targetId, actionType,
            NormalizeReason(reason), NormalizeNote(internalNote), createdAtUtc, expiresAtUtc);

    private static string NormalizeReason(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length is 0 or > MaximumReasonLength)
            throw new ArgumentException($"Reason must contain between 1 and {MaximumReasonLength} characters.");
        return normalized;
    }

    private static string? NormalizeNote(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized)) return null;
        if (normalized.Length > MaximumInternalNoteLength)
            throw new ArgumentException($"Internal note cannot exceed {MaximumInternalNoteLength} characters.");
        return normalized;
    }
}
