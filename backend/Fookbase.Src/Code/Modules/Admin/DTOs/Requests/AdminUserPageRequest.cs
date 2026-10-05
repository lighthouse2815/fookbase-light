using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Admin.DTOs.Requests;

public sealed record AdminUserPageRequest(
    string? Query = null,

    [Range(0, int.MaxValue, ErrorMessage = "Vị trí bắt đầu không được âm.")]
    int Offset = 0,

    [Range(1, IdentityModuleConstants.Administration.MaximumPageSize, ErrorMessage = "Số tài khoản phải từ {1} đến {2}.")]
    int Limit = 20);
