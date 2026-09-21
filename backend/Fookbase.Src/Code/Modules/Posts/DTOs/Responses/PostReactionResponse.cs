namespace Fookbase.Api.Modules.Posts.DTOs.Responses;

public sealed record PostReactionResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    string Type,
    string RelationshipStatus = "none",
    Guid? RelationshipRequestId = null);
