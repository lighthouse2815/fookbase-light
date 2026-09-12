namespace Fookbase.Api.Modules.Posts.DTOs.Responses;

public sealed record ContentMentionResponse(
    Guid UserId,
    string Username,
    int StartIndex,
    int Length);

public sealed record SavedPostsPageResponse(
    IReadOnlyList<PostResponse> Items,
    string? NextCursor);

public sealed record PostShareResponse(
    Guid Id,
    Guid SharingUserId,
    string DestinationType,
    Guid DestinationId,
    string? Caption,
    DateTimeOffset CreatedAtUtc,
    PostResponse OriginalPost);

public sealed record HashtagPostsPageResponse(
    string Tag,
    IReadOnlyList<PostResponse> Items,
    string? NextCursor);
