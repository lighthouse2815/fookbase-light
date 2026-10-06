using Fookbase.Api.Modules.Events.Common;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Posts.Entities;

namespace Fookbase.Api.Modules.Events.DTOs.Requests;

public sealed record CreateEventPostRequest(
    [EventPostContent(ErrorMessage = "Bài viết cần nội dung hoặc tệp đính kèm.")]
    [TrimmedStringLength(Post.MaximumContentLength, ErrorMessage = "Nội dung không được vượt quá {1} ký tự.")]
    string? Content,

    [EventPostMediaIds]
    IReadOnlyList<Guid>? MediaIds);
