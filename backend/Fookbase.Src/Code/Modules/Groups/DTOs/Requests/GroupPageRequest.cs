using Fookbase.Api.Modules.Groups.Common;
using Fookbase.Api.Modules.Groups.Services;
using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Groups.DTOs.Requests;

public sealed record GroupPageRequest(
    [CustomValidation(typeof(GroupPageRequest), nameof(GroupPageRequest.ValidateCursor))]
    string? Cursor = null,

    [Range(1, GroupsService.MaximumPageSize, ErrorMessage = "Số kết quả phải từ {1} đến {2}.")]
    int Limit = GroupsService.DefaultPageSize)
{
    public static ValidationResult? ValidateCursor(string? value)
    {
        try
        {
            _ = GroupCursor.DecodeOrNull(value);
            return ValidationResult.Success;
        }
        catch (FormatException)
        {
            return new ValidationResult("Con trỏ nhóm không hợp lệ.");
        }
    }
}
