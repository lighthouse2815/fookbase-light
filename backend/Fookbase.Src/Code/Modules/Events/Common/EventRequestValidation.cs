using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Events.Domain.Enums;
using Fookbase.Api.Modules.Events.DTOs.Requests;
using Fookbase.Api.Modules.Events.Entities;
using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Events.Common;

public static class EventRequestValidation
{
    public static ValidationResult? ValidateName(string? value) =>
        value?.Trim().Length > Event.MaximumNameLength
            ? new ValidationResult($"Tên sự kiện không được vượt quá {Event.MaximumNameLength} ký tự.")
            : ValidationResult.Success;

    public static ValidationResult? ValidateDescription(string? value) =>
        value?.Trim().Length > Event.MaximumDescriptionLength
            ? new ValidationResult($"Mô tả không được vượt quá {Event.MaximumDescriptionLength} ký tự.")
            : ValidationResult.Success;

    public static ValidationResult? ValidateHostType(string? value) =>
        IsEnum<EventHostType>(value)
            ? ValidationResult.Success
            : new ValidationResult("Loại chủ sự kiện phải là user, group hoặc page.");

    public static ValidationResult? ValidatePrivacy(string? value) =>
        IsEnum<EventPrivacy>(value)
            ? ValidationResult.Success
            : new ValidationResult("Quyền riêng tư phải là public hoặc private.");

    public static ValidationResult? ValidateLocationType(string? value) =>
        IsEnum<EventLocationType>(value)
            ? ValidationResult.Success
            : new ValidationResult("Địa điểm phải là physical hoặc online.");

    public static ValidationResult? ValidateInitialStatus(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        EnumText.TryParse(value, true, out EventStatus status) && status is EventStatus.DRAFT or EventStatus.PUBLISHED
            ? ValidationResult.Success
            : new ValidationResult("Trạng thái ban đầu phải là draft hoặc published.");

    public static ValidationResult? ValidateOptionalId(Guid? value) =>
        value == Guid.Empty ? new ValidationResult("Mã định danh không được để trống.") : ValidationResult.Success;

    public static ValidationResult? ValidateHostId(Guid? value, ValidationContext context)
    {
        var request = (CreateEventRequest)context.ObjectInstance;
        if (value is null && EnumText.TryParse(request.HostType, true, out EventHostType type) &&
            type is EventHostType.GROUP or EventHostType.PAGE)
        {
            return new ValidationResult("Mã nhóm hoặc trang tổ chức sự kiện là bắt buộc.");
        }

        return ValidateOptionalId(value);
    }

    public static ValidationResult? ValidateStartTime(DateTimeOffset value) =>
        value == default ? new ValidationResult("Thời gian bắt đầu là bắt buộc.") : ValidationResult.Success;

    public static ValidationResult? ValidateEndTime(DateTimeOffset? value, ValidationContext context)
    {
        var start = context.ObjectInstance switch
        {
            CreateEventRequest request => request.StartsAtUtc,
            UpdateEventRequest request => request.StartsAtUtc,
            _ => throw new InvalidOperationException("Unsupported event request.")
        };
        return value <= start
            ? new ValidationResult("Thời gian kết thúc phải sau thời gian bắt đầu.")
            : ValidationResult.Success;
    }

    public static ValidationResult? ValidateOnlineUrl(string? value, ValidationContext context)
    {
        var location = context.ObjectInstance switch
        {
            CreateEventRequest request => request.LocationType,
            UpdateEventRequest request => request.LocationType,
            _ => throw new InvalidOperationException("Unsupported event request.")
        };
        if (!EnumText.TryParse(location, true, out EventLocationType type) || type != EventLocationType.ONLINE)
        {
            return ValidationResult.Success;
        }

        return Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https"
                ? ValidationResult.Success
                : new ValidationResult("Sự kiện trực tuyến cần URL http hoặc https hợp lệ.");
    }

    private static bool IsEnum<T>(string? value) where T : struct, Enum =>
        EnumText.TryParse(value, true, out T parsed) && Enum.IsDefined(parsed);
}
