namespace Fookbase.Api.Modules.Friends.DTOs.Responses;

public sealed record FriendSuggestionResponse(
    FriendSuggestionProfileResponse Profile,
    int MutualFriendCount,
    int SharedGroupCount,
    int SharedPageCount,
    string RelationshipStatus,
    bool IsFollowing);
