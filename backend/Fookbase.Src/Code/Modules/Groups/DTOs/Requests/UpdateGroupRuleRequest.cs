namespace Fookbase.Api.Modules.Groups.DTOs.Requests;

public sealed record UpdateGroupRuleRequest(string Title, string? Description, int SortOrder);
