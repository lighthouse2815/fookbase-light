using Fookbase.Api.Modules.Events.Common;
using Fookbase.Api.Modules.Pages.Common;
using Fookbase.Api.Modules.Pages.Domain.Enums;

namespace Fookbase.Api.Modules.Pages.DTOs.Requests;

public sealed record SetPageMediaRequest(
    [OptionalNonEmptyGuid(ErrorMessage = "Mã ảnh đại diện không được để trống.")]
    [PageMediaNotRemoved(PageMediaSlot.AVATAR, ErrorMessage = "Không thể vừa cập nhật vừa xóa ảnh đại diện.")]
    Guid? AvatarMediaId,

    [OptionalNonEmptyGuid(ErrorMessage = "Mã ảnh bìa không được để trống.")]
    [PageMediaNotRemoved(PageMediaSlot.COVER, ErrorMessage = "Không thể vừa cập nhật vừa xóa ảnh bìa.")]
    Guid? CoverMediaId,

    bool RemoveAvatar = false,
    bool RemoveCover = false);
