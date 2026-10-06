using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Friends.Common;

namespace Fookbase.Api.Modules.Friends.DTOs.Requests;

public sealed record FriendSuggestionPageRequest(
    [ValidFriendSuggestionCursor(ErrorMessage = "Con trỏ gợi ý kết bạn không hợp lệ.")]
    string? Cursor = null,

    [Range(1, 50, ErrorMessage = "Số kết quả phải từ {1} đến {2}.")]
    [FriendSuggestionPageLimit]
    int? Limit = null);
