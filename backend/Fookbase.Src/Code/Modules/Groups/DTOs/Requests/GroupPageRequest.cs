using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Groups.Common;
using Fookbase.Api.Modules.Groups.Services;

namespace Fookbase.Api.Modules.Groups.DTOs.Requests;

public sealed record GroupPageRequest(
    [ValidGroupCursor(ErrorMessage = "Con trỏ nhóm không hợp lệ.")]
    string? Cursor = null,

    [Range(1, GroupsService.MaximumPageSize, ErrorMessage = "Số kết quả phải từ {1} đến {2}.")]
    int Limit = GroupsService.DefaultPageSize);
