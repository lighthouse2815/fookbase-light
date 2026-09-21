namespace Fookbase.Api.Modules.Users.DTOs.Requests;

public sealed record UpdatePrivacySettingsRequest(
    string? DefaultPostPrivacy,
    string? FriendRequestPolicy,
    string? FriendListVisibility,
    string? FollowListVisibility);
