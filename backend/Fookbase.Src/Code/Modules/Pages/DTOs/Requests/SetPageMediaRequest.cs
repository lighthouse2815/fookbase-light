using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Events.Common;

namespace Fookbase.Api.Modules.Pages.DTOs.Requests;

public sealed record SetPageMediaRequest(
    [OptionalNonEmptyGuid(ErrorMessage = "Mã ảnh đại diện không được để trống.")]
    Guid? AvatarMediaId,

    [OptionalNonEmptyGuid(ErrorMessage = "Mã ảnh bìa không được để trống.")]
    Guid? CoverMediaId,

    bool RemoveAvatar = false,
    bool RemoveCover = false) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RemoveAvatar && AvatarMediaId is not null)
        {
            yield return new ValidationResult("Không thể vừa cập nhật vừa xóa ảnh đại diện.", [nameof(AvatarMediaId)]);
        }

        if (RemoveCover && CoverMediaId is not null)
        {
            yield return new ValidationResult("Không thể vừa cập nhật vừa xóa ảnh bìa.", [nameof(CoverMediaId)]);
        }
    }
}
