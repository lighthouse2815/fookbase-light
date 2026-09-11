namespace Fookbase.Api.Modules.Stories.DTOs.Requests;

public sealed record CreateStoryRequest(Guid MediaId, string? Caption, string Privacy);
