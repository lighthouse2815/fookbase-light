namespace Fookbase.Api.Modules.Reels.DTOs.Responses;

public sealed record ReelAuthorResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl);
