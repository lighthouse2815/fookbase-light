using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Posts.Config;
using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.Extensions.Options;

namespace Fookbase.Api.Modules.Events.DTOs.Requests;

public sealed record CreateEventPostRequest(
    [CustomValidation(typeof(CreateEventPostRequest), nameof(CreateEventPostRequest.ValidateContent))]
    string? Content,

    [CustomValidation(typeof(CreateEventPostRequest), nameof(CreateEventPostRequest.ValidateMediaIds))]
    IReadOnlyList<Guid>? MediaIds)
{
    public static ValidationResult? ValidateContent(string? value, ValidationContext context)
    {
        var request = (CreateEventPostRequest)context.ObjectInstance;
        if (string.IsNullOrWhiteSpace(value) && request.MediaIds is not { Count: > 0 })
        {
            return new ValidationResult("Bài viết cần nội dung hoặc tệp đính kèm.");
        }

        return value?.Trim().Length > Post.MaximumContentLength
            ? new ValidationResult($"Nội dung không được vượt quá {Post.MaximumContentLength} ký tự.")
            : ValidationResult.Success;
    }

    public static ValidationResult? ValidateMediaIds(IReadOnlyList<Guid>? value, ValidationContext context)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        var maximum = (context.GetService(typeof(IOptions<PostsOptions>)) as IOptions<PostsOptions>)
            ?.Value.MaximumAttachments ?? new PostsOptions().MaximumAttachments;
        if (value.Count > maximum)
        {
            return new ValidationResult($"Bài viết chỉ được có tối đa {maximum} tệp đính kèm.");
        }

        return value.Contains(Guid.Empty) || value.Distinct().Count() != value.Count
            ? new ValidationResult("Mã tệp đính kèm phải hợp lệ và không được trùng lặp.")
            : ValidationResult.Success;
    }
}
