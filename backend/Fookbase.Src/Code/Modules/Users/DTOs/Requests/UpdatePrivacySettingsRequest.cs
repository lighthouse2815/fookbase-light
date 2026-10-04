using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Users.Common;
using Fookbase.Api.Modules.Users.Domain.Enums;

namespace Fookbase.Api.Modules.Users.DTOs.Requests;

public sealed record UpdatePrivacySettingsRequest(

    [property: OptionalEnumValue<PostPrivacy>(ErrorMessage = "Quyền riêng tư mặc định của bài viết không hợp lệ.")]
    string? DefaultPostPrivacy,

    [property: OptionalEnumValue<FriendRequestPolicy>(ErrorMessage = "Chính sách lời mời kết bạn không hợp lệ.")]
    string? FriendRequestPolicy,

    [property: OptionalEnumValue<RelationshipListVisibility>(ErrorMessage = "Quyền hiển thị danh sách bạn bè không hợp lệ.")]
    string? FriendListVisibility,

    [property: OptionalEnumValue<RelationshipListVisibility>(ErrorMessage = "Quyền hiển thị danh sách theo dõi không hợp lệ.")]
    string? FollowListVisibility);
