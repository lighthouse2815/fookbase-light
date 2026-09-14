using Fookbase.Api.Modules.Posts.DTOs.Responses;

namespace Fookbase.Api.Modules.Groups.DTOs.Responses;

public sealed record GroupFeedItemResponse(GroupResponse Group, PostResponse Post);

public sealed record GroupFeedPageResponse(
    IReadOnlyList<GroupFeedItemResponse> Items,
    string? NextCursor);
