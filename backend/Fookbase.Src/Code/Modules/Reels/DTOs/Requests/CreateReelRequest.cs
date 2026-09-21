namespace Fookbase.Api.Modules.Reels.DTOs.Requests;

public sealed record CreateReelRequest(string? Caption, string Privacy, Guid VideoMediaId);
