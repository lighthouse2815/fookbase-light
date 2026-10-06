using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Notifications.Common;
using Fookbase.Api.Modules.Notifications.Services;

namespace Fookbase.Api.Modules.Notifications.DTOs.Requests;

public sealed record NotificationPageRequest(
    [ValidNotificationCursor(ErrorMessage = "Con trỏ thông báo không hợp lệ.")]
    string? Before = null,

    [Range(1, NotificationService.MaximumPageSize, ErrorMessage = "Số thông báo phải từ {1} đến {2}.")]
    int Limit = NotificationService.DefaultPageSize);
