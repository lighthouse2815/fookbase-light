using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Modules.Users.Common;
using Fookbase.Api.Modules.Users.Domain.Enums;

namespace Fookbase.Api.Modules.Users.DTOs.Requests;

public sealed record UpdatePrivacySettingsRequest(

    [OptionalEnumValue<PostPrivacy>(ErrorMessage = "Quyền riêng tư mặc định của bài viết không hợp lệ.")]
    string? DefaultPostPrivacy,

    [OptionalEnumValue<FriendRequestPolicy>(ErrorMessage = "Chính sách lời mời kết bạn không hợp lệ.")]
    string? FriendRequestPolicy,

    [OptionalEnumValue<RelationshipListVisibility>(ErrorMessage = "Quyền hiển thị danh sách bạn bè không hợp lệ.")]
    string? FriendListVisibility,

    [OptionalEnumValue<RelationshipListVisibility>(ErrorMessage = "Quyền hiển thị danh sách theo dõi không hợp lệ.")]
    string? FollowListVisibility);
