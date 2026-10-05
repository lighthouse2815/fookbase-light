namespace Fookbase.Api.Modules.Admin.DTOs.Responses;

public sealed record ModerationActionPageResponse(
    IReadOnlyList<ModerationActionResponse> Items,
    string? NextCursor);
