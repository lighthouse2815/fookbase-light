using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Media.Config;
using Fookbase.Api.Modules.Media.Domain.Enums;
using Fookbase.Api.Modules.Media.DTOs.Requests;
using Fookbase.Api.Modules.Media.Entities;

namespace Fookbase.Api.Modules.Media.Common;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class ValidMediaFileNameAttribute() : ValidationAttribute(
    $"Tên tệp phải có từ 1 đến {MediaAsset.MaximumFileNameLength} ký tự.")
{
    public override bool IsValid(object? value)
    {
        var fileName = Path.GetFileName(value as string ?? string.Empty);
        return !string.IsNullOrWhiteSpace(fileName) && fileName.Length <= MediaAsset.MaximumFileNameLength;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class SupportedMediaContentTypeAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is string text && MediaFormat.SupportedFormats.ContainsKey(text.Trim());
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class MediaUploadSizeLimitAttribute : ValidationAttribute
{
    public override bool RequiresValidationContext => true;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var request = (CreateUploadRequest)validationContext.ObjectInstance;
        if (request.ContentType is null ||
            !MediaFormat.SupportedFormats.TryGetValue(request.ContentType.Trim(), out var format))
        {
            return ValidationResult.Success;
        }

        var options = (MediaOptions?)validationContext.GetService(typeof(MediaOptions))
            ?? throw new InvalidOperationException("Media options are required for upload validation.");
        var maximumSize = format.MediaType == MediaType.IMAGE
            ? options.MaximumImageSizeBytes
            : options.MaximumVideoSizeBytes;
        return (long)value! > maximumSize
            ? new ValidationResult($"Dung lượng tệp không được vượt quá {maximumSize} byte.")
            : ValidationResult.Success;
    }
}
