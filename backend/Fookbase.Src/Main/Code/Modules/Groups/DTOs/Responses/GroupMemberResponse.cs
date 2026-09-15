namespace Fookbase.Api.Modules.Groups.DTOs.Responses;

public sealed record GroupMemberResponse(
    Guid UserId,
    string Role,
    DateTimeOffset JoinedAtUtc,
    string? Username = null,
    string? DisplayName = null,
    string? AvatarUrl = null);
