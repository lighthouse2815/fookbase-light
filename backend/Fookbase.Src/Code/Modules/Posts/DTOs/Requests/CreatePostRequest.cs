using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Posts.DTOs.Requests;

public sealed record CreatePostRequest(
    [TrimmedStringLength(10_000, ErrorMessage = "Nội dung bài viết không được vượt quá {1} ký tự.")]
    string Content,

    [OptionalEnumValue<PostPrivacy>(ErrorMessage = "Quyền riêng tư phải là public, friends hoặc onlyMe.")]
    string? Privacy = null,

    IReadOnlyList<Guid>? MediaIds = null,
    string? TextBackground = null);
