namespace Fookbase.Api.Modules.Admin.DTOs.Requests;

public sealed record SuspendUserRequest(string? Reason, int? DurationHours, DateTimeOffset? SuspendedUntilUtc, string? InternalNote);
