using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Events.Common;

namespace Fookbase.Api.Modules.Events.DTOs.Requests;

public sealed record CreateEventRequest(
    [Required(ErrorMessage = "Loại chủ sự kiện là bắt buộc.")]
    [CustomValidation(typeof(EventRequestValidation), nameof(EventRequestValidation.ValidateHostType))]
    string HostType,

    [CustomValidation(typeof(EventRequestValidation), nameof(EventRequestValidation.ValidateHostId))]
    Guid? HostId,

    [Required(ErrorMessage = "Tên sự kiện là bắt buộc.")]
    [CustomValidation(typeof(EventRequestValidation), nameof(EventRequestValidation.ValidateName))]
    string Name,

    [CustomValidation(typeof(EventRequestValidation), nameof(EventRequestValidation.ValidateDescription))]
    string? Description,

    [Required(ErrorMessage = "Quyền riêng tư là bắt buộc.")]
    [CustomValidation(typeof(EventRequestValidation), nameof(EventRequestValidation.ValidatePrivacy))]
    string Privacy,

    [Required(ErrorMessage = "Loại địa điểm là bắt buộc.")]
    [CustomValidation(typeof(EventRequestValidation), nameof(EventRequestValidation.ValidateLocationType))]
    string LocationType,
    string? LocationName,
    string? Address,

    [CustomValidation(typeof(EventRequestValidation), nameof(EventRequestValidation.ValidateOnlineUrl))]
    string? OnlineUrl,

    [CustomValidation(typeof(EventRequestValidation), nameof(EventRequestValidation.ValidateStartTime))]
    DateTimeOffset StartsAtUtc,

    [CustomValidation(typeof(EventRequestValidation), nameof(EventRequestValidation.ValidateEndTime))]
    DateTimeOffset? EndsAtUtc,

    [CustomValidation(typeof(EventRequestValidation), nameof(EventRequestValidation.ValidateOptionalId))]
    Guid? CoverMediaId,

    [CustomValidation(typeof(EventRequestValidation), nameof(EventRequestValidation.ValidateInitialStatus))]
    string? Status = null);
