namespace Fookbase.Api.Modules.Friends.DTOs.Responses;

public sealed record BlockedUserResponse(Guid UserId, DateTimeOffset BlockedAtUtc);
