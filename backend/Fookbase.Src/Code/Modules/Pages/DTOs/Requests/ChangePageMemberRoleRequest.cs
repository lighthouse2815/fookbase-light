using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Pages.DTOs.Requests;

public sealed record ChangePageMemberRoleRequest(
    [Required(ErrorMessage = "Vai trò trên trang là bắt buộc.")]
    string Role);
