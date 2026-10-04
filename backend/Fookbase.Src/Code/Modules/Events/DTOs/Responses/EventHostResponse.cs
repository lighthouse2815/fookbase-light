namespace Fookbase.Api.Modules.Events.DTOs.Responses;

public sealed record EventHostResponse(string Type, Guid Id, string Name, string? Username = null, string? AvatarUrl = null);
