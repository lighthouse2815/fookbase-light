namespace Fookbase.Api.Modules.Groups.DTOs.Requests;

public sealed record CreateGroupRequest(string Name, string? Description, string Privacy);
