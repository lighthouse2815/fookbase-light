namespace Fookbase.Api.Modules.Identity.DTOs.Responses;

public sealed record AdminUserResponse(
    Guid Id,
    string Email,
    string Username,
    bool IsActive,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> Roles,
    int WarningCount = 0,
    DateTimeOffset? SuspendedUntilUtc = null,
    DateTimeOffset? ModerationDisabledAtUtc = null);
