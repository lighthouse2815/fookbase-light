namespace Fookbase.Api.Modules.Pages.DTOs.Responses;

public sealed record PageMemberResponse(Guid UserId, string Username, string DisplayName, string Role,
    DateTimeOffset JoinedAtUtc);
