using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Friends.Common;
using Fookbase.Api.Modules.Friends.Services;

namespace Fookbase.Api.Modules.Friends.DTOs.Requests;

public sealed record FollowPageRequest(
    [ValidFollowCursor(ErrorMessage = "Con trỏ danh sách theo dõi không hợp lệ.")]
    string? Cursor = null,

    [Range(1, FriendsService.MaximumFollowPageSize, ErrorMessage = "Số kết quả phải từ {1} đến {2}.")]
    int Limit = FriendsService.DefaultFollowPageSize);
