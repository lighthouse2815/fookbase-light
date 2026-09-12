namespace Fookbase.Api.Modules.Posts.DTOs.Requests;

public sealed record CreatePostShareRequest(
    string DestinationType,
    Guid DestinationId,
    string? Caption);
