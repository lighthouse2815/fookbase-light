using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Reels.Services;

namespace Fookbase.Api.Modules.Reels.DTOs.Requests;

public sealed record ReelFeedRequest(
    [RegularExpression(@"(?i)^(\s*|forYou|following)$", ErrorMessage = "Chế độ reel phải là forYou hoặc following.")]
    string? Mode = null,
    string? Cursor = null,

    [Range(1, ReelsService.MaximumPageSize, ErrorMessage = "Số kết quả phải từ {1} đến {2}.")]
    int Limit = ReelsService.DefaultPageSize);
