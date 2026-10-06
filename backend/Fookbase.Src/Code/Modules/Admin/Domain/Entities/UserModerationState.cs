using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;

namespace Fookbase.Api.Modules.Admin.Entities;

public sealed class UserModerationState
{
    private UserModerationState() { }

    public UserModerationState(Guid userId, DateTimeOffset now)
    {
        UserId = userId;
        UpdatedAtUtc = now;
    }

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Guid UserId { get; private set; }

    public User User { get; private set; } = null!;

    public int WarningCount { get; private set; }

    public DateTimeOffset? SuspendedUntilUtc { get; private set; }

    public DateTimeOffset? DisabledAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public bool IsDisabled => DisabledAtUtc is not null;

    public bool IsSuspendedAt(DateTimeOffset now) => SuspendedUntilUtc is { } until && until > now;

    public void Warn(DateTimeOffset now)
    {
        WarningCount++;
        UpdatedAtUtc = now;
    }

    public void Suspend(DateTimeOffset until, DateTimeOffset now)
    {
        SuspendedUntilUtc = until;
        UpdatedAtUtc = now;
    }

    public void Unsuspend(DateTimeOffset now)
    {
        SuspendedUntilUtc = null;
        UpdatedAtUtc = now;
    }

    public void Disable(DateTimeOffset now)
    {
        DisabledAtUtc ??= now;
        UpdatedAtUtc = now;
    }

    public void Enable(DateTimeOffset now)
    {
        DisabledAtUtc = null;
        UpdatedAtUtc = now;
    }
}
