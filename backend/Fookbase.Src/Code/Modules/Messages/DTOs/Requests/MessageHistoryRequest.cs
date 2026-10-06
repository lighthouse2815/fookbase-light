using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Messages.Common;
using Fookbase.Api.Modules.Messages.Services;

namespace Fookbase.Api.Modules.Messages.DTOs.Requests;

public sealed record MessageHistoryRequest(
    [ValidMessageCursor<MessageCursor>(ErrorMessage = "Con trỏ phân trang không hợp lệ.")]
    string? Before = null,

    [Range(1, MessagesService.MaximumLimit)]
    int Limit = 50);
