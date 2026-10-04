using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Notifications.Entities;

namespace Fookbase.Api.Modules.Notifications.DTOs.Requests;

internal sealed record ExpoPushMessage(
    [Required]
    [MaxLength(PushDevice.MaximumExpoPushTokenLength)]
    string To,
    [Required] string Title,
    [Required] string Body,
    [Required] string Sound,
    [Required] object Data);
