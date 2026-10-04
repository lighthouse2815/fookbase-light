using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Groups.DTOs.Requests;

public sealed record CreateGroupInviteRequest(
    [NonEmptyGuid(ErrorMessage = "Người được mời là bắt buộc.")]
    Guid UserId);
