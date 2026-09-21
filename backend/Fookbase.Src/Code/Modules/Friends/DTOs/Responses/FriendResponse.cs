namespace Fookbase.Api.Modules.Friends.DTOs.Responses;

public sealed record FriendResponse(Guid UserId, DateTimeOffset FriendsSinceUtc);
