using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Friends.Services;

namespace Fookbase.Api.Modules.Friends.DTOs.Requests;

public sealed record FriendPageRequest(
    [Range(0, int.MaxValue, ErrorMessage = "Vị trí bắt đầu phải không âm.")]
    int Offset = 0,

    [Range(1, FriendsService.MaximumPageSize, ErrorMessage = "Số kết quả phải từ {1} đến {2}.")]
    int Limit = FriendsService.DefaultPageSize);
