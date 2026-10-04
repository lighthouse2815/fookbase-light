namespace Fookbase.Api.Modules.Stories.DTOs.Responses;

public sealed record StoryViewerResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    DateTimeOffset ViewedAtUtc,
    string? ReactionType);
