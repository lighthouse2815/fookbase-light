using Fookbase.Api.Modules.Events.Common;
using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Events.DTOs.Requests;

public sealed record CreateEventPostRequest(
    [EventPostContent(ErrorMessage = "Bài viết cần nội dung hoặc tệp đính kèm.")]
    [TrimmedStringLength(10_000, ErrorMessage = "Nội dung không được vượt quá {1} ký tự.")]
    string? Content,

    [EventPostMediaIds]
    IReadOnlyList<Guid>? MediaIds);
