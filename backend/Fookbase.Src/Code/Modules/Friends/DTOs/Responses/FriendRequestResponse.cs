namespace Fookbase.Api.Modules.Friends.DTOs.Responses;

public sealed record FriendRequestResponse(
    Guid Id,
    Guid SenderUserId,
    Guid ReceiverUserId,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RespondedAtUtc);
