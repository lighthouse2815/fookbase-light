namespace Fookbase.Api.Modules.Feed.DTOs.Responses;

public sealed record FeedContainerResponse(
    Guid Id,
    string Name,
    string? Username,
    string? Privacy);
