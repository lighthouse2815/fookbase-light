namespace Fookbase.Api.Modules.Friends.DTOs.Responses;

public sealed record FriendSuggestionProfileResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl);
