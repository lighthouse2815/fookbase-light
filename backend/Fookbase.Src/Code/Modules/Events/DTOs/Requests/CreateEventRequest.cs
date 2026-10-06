using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Events.Common;
using Fookbase.Api.Modules.Events.Domain.Enums;
using Fookbase.Api.Modules.Events.Entities;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Events.DTOs.Requests;

public sealed record CreateEventRequest(
    [Required(ErrorMessage = "Loại chủ sự kiện là bắt buộc.")]
    [OptionalEnumValue<EventHostType>(ErrorMessage = "Loại chủ sự kiện phải là user, group hoặc page.")]
    string HostType,

    [RequiredEventHostId(ErrorMessage = "Mã nhóm hoặc trang tổ chức sự kiện là bắt buộc.")]
    [OptionalNonEmptyGuid(ErrorMessage = "Mã định danh không được để trống.")]
    Guid? HostId,

    [Required(ErrorMessage = "Tên sự kiện là bắt buộc.")]
    [TrimmedStringLength(Event.MaximumNameLength, ErrorMessage = "Tên sự kiện không được vượt quá {1} ký tự.")]
    string Name,

    [TrimmedStringLength(Event.MaximumDescriptionLength, ErrorMessage = "Mô tả không được vượt quá {1} ký tự.")]
    string? Description,

    [Required(ErrorMessage = "Quyền riêng tư là bắt buộc.")]
    [OptionalEnumValue<EventPrivacy>(ErrorMessage = "Quyền riêng tư phải là public hoặc private.")]
    string Privacy,

    [Required(ErrorMessage = "Loại địa điểm là bắt buộc.")]
    [OptionalEnumValue<EventLocationType>(ErrorMessage = "Địa điểm phải là physical hoặc online.")]
    string LocationType,
    string? LocationName,
    string? Address,

    [EventOnlineUrl(ErrorMessage = "Sự kiện trực tuyến cần URL http hoặc https hợp lệ.")]
    string? OnlineUrl,

    [EventStartTime(ErrorMessage = "Thời gian bắt đầu là bắt buộc.")]
    DateTimeOffset StartsAtUtc,

    [EventEndTime(ErrorMessage = "Thời gian kết thúc phải sau thời gian bắt đầu.")]
    DateTimeOffset? EndsAtUtc,

    [OptionalNonEmptyGuid(ErrorMessage = "Mã định danh không được để trống.")]
    Guid? CoverMediaId,

    [InitialEventStatus(ErrorMessage = "Trạng thái ban đầu phải là draft hoặc published.")]
    string? Status = null);
