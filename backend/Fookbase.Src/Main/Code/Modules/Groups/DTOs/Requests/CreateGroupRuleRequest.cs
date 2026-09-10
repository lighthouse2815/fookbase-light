namespace Fookbase.Api.Modules.Groups.DTOs.Requests;

public sealed record CreateGroupRuleRequest(string Title, string? Description, int SortOrder);
