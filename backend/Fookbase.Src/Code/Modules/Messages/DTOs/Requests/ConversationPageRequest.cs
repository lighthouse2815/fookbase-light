using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Messages.Common;
using Fookbase.Api.Modules.Messages.Services;

namespace Fookbase.Api.Modules.Messages.DTOs.Requests;

public sealed record ConversationPageRequest(
    [ValidMessageCursor<ConversationCursor>(ErrorMessage = "Con trỏ phân trang không hợp lệ.")]
    string? Before = null,

    [Range(1, MessagesService.MaximumLimit)]
    int Limit = 20,
    bool IncludeArchived = false);
