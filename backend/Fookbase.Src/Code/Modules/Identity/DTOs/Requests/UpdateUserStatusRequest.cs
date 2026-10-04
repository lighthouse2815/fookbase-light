using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record UpdateUserStatusRequest(

    [Required(ErrorMessage = "Trạng thái hoạt động là bắt buộc.")]
    bool? IsActive);
