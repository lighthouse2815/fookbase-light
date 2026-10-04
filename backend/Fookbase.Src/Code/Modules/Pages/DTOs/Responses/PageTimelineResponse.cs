using Fookbase.Api.Modules.Posts.DTOs.Responses;

namespace Fookbase.Api.Modules.Pages.DTOs.Responses;

public sealed record PageTimelineResponse(IReadOnlyList<PostResponse> Items, string? NextCursor);
