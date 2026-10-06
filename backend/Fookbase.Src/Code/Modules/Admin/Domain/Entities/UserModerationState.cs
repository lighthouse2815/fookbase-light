using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Admin.Entities;

public sealed class UserModerationState
{
    private UserModerationState() { }

    private UserModerationState(Guid userId, DateTimeOffset now)
    {
        UserId = userId;
        UpdatedAtUtc = now;
    }

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Guid UserId { get; private set; }
    public int WarningCount { get; private set; }
    public DateTimeOffset? SuspendedUntilUtc { get; private set; }
    public DateTimeOffset? DisabledAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [ForeignKey(nameof(UserId))]
    [InverseProperty(nameof(User.ModerationState))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public User User { get; private set; } = null!;

    public static UserModerationState Create(Guid userId, DateTimeOffset now) => new(userId, now);
    public bool IsSuspendedAt(DateTimeOffset now) => SuspendedUntilUtc is { } until && until > now;
    public bool IsDisabled => DisabledAtUtc is not null;
    public void Warn(DateTimeOffset now) { WarningCount++; UpdatedAtUtc = now; }
    public void Suspend(DateTimeOffset until, DateTimeOffset now) { SuspendedUntilUtc = until; UpdatedAtUtc = now; }
    public void Unsuspend(DateTimeOffset now) { SuspendedUntilUtc = null; UpdatedAtUtc = now; }
    public void Disable(DateTimeOffset now) { DisabledAtUtc ??= now; UpdatedAtUtc = now; }
    public void Enable(DateTimeOffset now) { DisabledAtUtc = null; UpdatedAtUtc = now; }
}
