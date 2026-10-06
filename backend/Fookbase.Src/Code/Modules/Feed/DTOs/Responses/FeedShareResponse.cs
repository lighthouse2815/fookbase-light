using Fookbase.Api.Modules.Posts.DTOs.Responses;

namespace Fookbase.Api.Modules.Feed.DTOs.Responses;

public sealed record FeedShareResponse(
    Guid Id,
    Guid OriginalPostId,
    string? Caption,
    DateTimeOffset CreatedAtUtc,
    FeedAuthorResponse Actor,
    PostDisplayIdentityResponse OriginalAuthor,
    PostResponse OriginalPost);
