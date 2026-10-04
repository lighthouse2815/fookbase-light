using Fookbase.Api.Modules.Posts.DTOs.Responses;

namespace Fookbase.Api.Modules.Search.DTOs.Responses;

public sealed record SearchPostResponse(
    Guid PostId,
    Guid? AuthorUserId,
    PostDisplayIdentityResponse? DisplayAuthor,
    string Snippet,
    IReadOnlyList<Guid> MediaIds,
    int CommentCount,
    IReadOnlyDictionary<string, int> ReactionCounts,
    string ContainerType,
    Guid ContainerId,
    DateTimeOffset CreatedAtUtc);
