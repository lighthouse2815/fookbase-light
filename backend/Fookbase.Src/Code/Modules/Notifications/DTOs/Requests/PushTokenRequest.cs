using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Notifications.Entities;

namespace Fookbase.Api.Modules.Notifications.DTOs.Requests;

public sealed record PushTokenRequest(
    [Required(ErrorMessage = "Push token là bắt buộc.")]
    [MaxLength(PushDevice.MaximumExpoPushTokenLength, ErrorMessage = "Push token không được vượt quá {1} ký tự.")]
    [RegularExpression(@"\A(?:Exponent|Expo)PushToken\[[A-Za-z0-9_-]+\]\z", ErrorMessage = "Expo push token không hợp lệ.")]
    string Token);
