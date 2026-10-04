using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Messages.Services;

namespace Fookbase.Api.Modules.Messages.DTOs.Requests;

public sealed record MessageNotificationPageRequest(
    [Range(0, int.MaxValue)] int Offset = 0,
    [Range(1, MessagesService.MaximumLimit)] int Limit = MessagesService.MaximumLimit);
