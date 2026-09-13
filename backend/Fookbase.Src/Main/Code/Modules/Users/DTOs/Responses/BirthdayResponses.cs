namespace Fookbase.Api.Modules.Users.DTOs.Responses;

public sealed record BirthdayFriendResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    int Month,
    int Day,
    string FriendshipState = "friends");
