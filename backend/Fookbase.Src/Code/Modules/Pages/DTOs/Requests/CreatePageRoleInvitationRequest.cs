using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Pages.DTOs.Requests;

public sealed record CreatePageRoleInvitationRequest(
    [NonEmptyGuid(ErrorMessage = "Người dùng là bắt buộc.")]
    Guid UserId,

    [Required(ErrorMessage = "Vai trò trên trang là bắt buộc.")]
    string Role);
