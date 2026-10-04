namespace Fookbase.Api.Modules.Search.DTOs.Responses;

public sealed record SearchGroupResponse(
    Guid GroupId,
    string Name,
    string? Description,
    string Privacy,
    string? CoverUrl,
    int MemberCount,
    string? ViewerMembershipState);
