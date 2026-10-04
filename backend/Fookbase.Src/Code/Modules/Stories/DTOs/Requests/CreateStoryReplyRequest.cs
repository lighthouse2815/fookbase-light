using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Messages.Services;

namespace Fookbase.Api.Modules.Stories.DTOs.Requests;

public sealed record CreateStoryReplyRequest(
    [Required(ErrorMessage = "Nội dung trả lời story là bắt buộc.")]
    [TrimmedStringLength(MessagesService.MaximumContentLength,
        ErrorMessage = "Nội dung trả lời story không được vượt quá {1} ký tự.")]
    string? Content);
