namespace Fookbase.Api.Modules.Feed.DTOs.Responses;

public sealed record FeedMediaResponse(
    Guid MediaId,
    string MediaType,
    string ContentType);
