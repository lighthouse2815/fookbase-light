using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Events.Domain.Enums;
using Fookbase.Api.Modules.Events.DTOs.Requests;
using Fookbase.Api.Modules.Posts.Config;
using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Events.Common;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class OptionalNonEmptyGuidAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is null || value is Guid id && id != Guid.Empty;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class RequiredEventHostIdAttribute : ValidationAttribute
{
    public override bool RequiresValidationContext => true;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var request = (CreateEventRequest)validationContext.ObjectInstance;
        return value is null && EnumText.TryParse(request.HostType, true, out EventHostType type) &&
            type is EventHostType.GROUP or EventHostType.PAGE
                ? new ValidationResult(FormatErrorMessage(validationContext.DisplayName))
                : ValidationResult.Success;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class InitialEventStatusAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is null ||
        value is string text &&
        (string.IsNullOrWhiteSpace(text) ||
         EnumText.TryParse(text, true, out EventStatus status) && status is EventStatus.DRAFT or EventStatus.PUBLISHED);
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class EventStartTimeAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is DateTimeOffset start && start != default;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class EventEndTimeAttribute : ValidationAttribute
{
    public override bool RequiresValidationContext => true;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var start = validationContext.ObjectInstance switch
        {
            CreateEventRequest request => request.StartsAtUtc,
            UpdateEventRequest request => request.StartsAtUtc,
            _ => throw new InvalidOperationException("Unsupported event request.")
        };
        return (DateTimeOffset?)value <= start
            ? new ValidationResult(FormatErrorMessage(validationContext.DisplayName))
            : ValidationResult.Success;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class EventOnlineUrlAttribute : ValidationAttribute
{
    public override bool RequiresValidationContext => true;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var location = validationContext.ObjectInstance switch
        {
            CreateEventRequest request => request.LocationType,
            UpdateEventRequest request => request.LocationType,
            _ => throw new InvalidOperationException("Unsupported event request.")
        };
        if (!EnumText.TryParse(location, true, out EventLocationType type) || type != EventLocationType.ONLINE)
        {
            return ValidationResult.Success;
        }

        return Uri.TryCreate((value as string)?.Trim(), UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https"
                ? ValidationResult.Success
                : new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class EventPostContentAttribute : ValidationAttribute
{
    public override bool RequiresValidationContext => true;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var request = (CreateEventPostRequest)validationContext.ObjectInstance;
        return string.IsNullOrWhiteSpace(value as string) && request.MediaIds is not { Count: > 0 }
            ? new ValidationResult(FormatErrorMessage(validationContext.DisplayName))
            : ValidationResult.Success;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class EventPostMediaIdsAttribute : ValidationAttribute
{
    public override bool RequiresValidationContext => true;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        var mediaIds = (IReadOnlyList<Guid>)value;
        var maximum = (validationContext.GetService(typeof(PostsOptions)) as PostsOptions)
            ?.MaximumAttachments ?? new PostsOptions().MaximumAttachments;
        if (mediaIds.Count > maximum)
        {
            return new ValidationResult($"Bài viết chỉ được có tối đa {maximum} tệp đính kèm.");
        }

        return mediaIds.Contains(Guid.Empty) || mediaIds.Distinct().Count() != mediaIds.Count
            ? new ValidationResult("Mã tệp đính kèm phải hợp lệ và không được trùng lặp.")
            : ValidationResult.Success;
    }
}
