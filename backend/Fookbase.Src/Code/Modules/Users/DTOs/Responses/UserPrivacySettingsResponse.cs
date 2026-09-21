namespace Fookbase.Api.Modules.Users.DTOs.Responses;

public sealed record UserPrivacySettingsResponse(
    string DefaultPostPrivacy,
    string FriendRequestPolicy,
    string FriendListVisibility,
    string FollowListVisibility,
    DateTimeOffset UpdatedAtUtc);
