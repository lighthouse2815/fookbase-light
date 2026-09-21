namespace Fookbase.Api.Modules.Groups.DTOs.Requests;

public sealed record UpdateGroupRequest(
    string Name,
    string? Description,
    string Privacy,
    Guid? CoverMediaId,
    bool RemoveCover = false);
