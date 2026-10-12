using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Posts.DTOs.Requests;

public sealed record UpdateCommentRequest(
    [Required(ErrorMessage = "Nội dung bình luận là bắt buộc.")]
    [TrimmedStringLength(5_000, ErrorMessage = "Nội dung bình luận không được vượt quá {1} ký tự.")]
    string Content);
