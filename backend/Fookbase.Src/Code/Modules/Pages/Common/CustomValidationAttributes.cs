using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Pages.Domain.Enums;
using Fookbase.Api.Modules.Pages.DTOs.Requests;

namespace Fookbase.Api.Modules.Pages.Common;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class PageMediaNotRemovedAttribute(PageMediaSlot slot) : ValidationAttribute
{
    public override bool RequiresValidationContext => true;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var request = (SetPageMediaRequest)validationContext.ObjectInstance;
        var remove = slot switch
        {
            PageMediaSlot.AVATAR => request.RemoveAvatar,
            PageMediaSlot.COVER => request.RemoveCover,
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        };
        return value is not null && remove
            ? new ValidationResult(FormatErrorMessage(validationContext.DisplayName))
            : ValidationResult.Success;
    }
}
