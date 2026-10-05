namespace Fookbase.Api.Modules.Admin.DTOs.Responses;

public sealed record UserModerationStateResponse(
    Guid UserId,
    int WarningCount,
    DateTimeOffset? SuspendedUntilUtc,
    DateTimeOffset? DisabledAtUtc,
    DateTimeOffset UpdatedAtUtc);
