namespace Fookbase.Api.Modules.Reels.DTOs.Responses;

public sealed record ReelPageResponse(IReadOnlyList<ReelResponse> Items, string? NextCursor);
