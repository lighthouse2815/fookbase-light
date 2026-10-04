using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Media.Common;
using Fookbase.Api.Modules.Media.Config;
using Fookbase.Api.Modules.Media.Domain.Enums;
using Fookbase.Api.Modules.Media.Entities;

namespace Fookbase.Api.Modules.Media.DTOs.Requests;

public sealed record CreateUploadRequest(
    [Required]
    [CustomValidation(typeof(CreateUploadRequest), nameof(CreateUploadRequest.ValidateFileName))]
    string FileName,

    [Required]
    [CustomValidation(typeof(CreateUploadRequest), nameof(CreateUploadRequest.ValidateContentType))]
    string ContentType,

    [Range(typeof(long), "1", "9223372036854775807")]
    long SizeBytes) : IValidatableObject
{
    public static ValidationResult? ValidateFileName(string? value)
    {
        var fileName = Path.GetFileName(value ?? string.Empty);
        return !string.IsNullOrWhiteSpace(fileName) && fileName.Length <= MediaAsset.MaximumFileNameLength
            ? ValidationResult.Success
            : new ValidationResult($"Tên tệp phải có từ 1 đến {MediaAsset.MaximumFileNameLength} ký tự.");
    }

    public static ValidationResult? ValidateContentType(string? value) =>
        value is not null && MediaFormat.SupportedFormats.ContainsKey(value.Trim())
            ? ValidationResult.Success
            : new ValidationResult("Định dạng hỗ trợ: JPEG, PNG, WebP, MP4 và WebM.");

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var options = (MediaOptions?)validationContext.GetService(typeof(MediaOptions))
            ?? throw new InvalidOperationException("Media options are required for upload validation.");
        var format = MediaFormat.SupportedFormats[ContentType.Trim()];
        var maximumSize = format.MediaType == MediaType.IMAGE
            ? options.MaximumImageSizeBytes
            : options.MaximumVideoSizeBytes;
        if (SizeBytes > maximumSize)
        {
            yield return new ValidationResult($"Dung lượng tệp không được vượt quá {maximumSize} byte.",
                [nameof(SizeBytes)]);
        }
    }
}
