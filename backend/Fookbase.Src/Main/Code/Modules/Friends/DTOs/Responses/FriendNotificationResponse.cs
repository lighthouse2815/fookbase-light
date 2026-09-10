namespace Fookbase.Api.Modules.Friends.DTOs.Responses;

public sealed record FriendNotificationResponse(
    Guid Id,
    Guid ActorUserId,
    Guid FriendRequestId,
    string Type,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc);
