namespace Fookbase.Api.Modules.Groups.DTOs.Responses;

public sealed record GroupRuleResponse(
    Guid Id,
    Guid GroupId,
    string Title,
    string? Description,
    int SortOrder);
