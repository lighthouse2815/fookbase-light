using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Ai.Config;
using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Ai.Common;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class AiInputLengthAttribute : ValidationAttribute
{
    public override bool RequiresValidationContext => true;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var options = (AiChatOptions?)validationContext.GetService(typeof(AiChatOptions))
            ?? throw new InvalidOperationException("Cấu hình trò chuyện AI chưa được đăng ký.");
        return new TrimmedStringLengthAttribute(options.MaximumInputCharacters).IsValid(value)
            ? ValidationResult.Success
            : new ValidationResult($"Nội dung tin nhắn không được vượt quá {options.MaximumInputCharacters} ký tự.",
                validationContext.MemberName is null ? null : [validationContext.MemberName]);
    }
}
