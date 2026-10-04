namespace Fookbase.Api.Modules.Search.DTOs.Responses;

public sealed record SearchReelResponse(
    Guid ReelId,
    SearchReelAuthorResponse Author,
    string Snippet,
    SearchReelMediaResponse Media,
    int CommentCount,
    int ReactionCount,
    long ViewCount,
    DateTimeOffset CreatedAtUtc);
