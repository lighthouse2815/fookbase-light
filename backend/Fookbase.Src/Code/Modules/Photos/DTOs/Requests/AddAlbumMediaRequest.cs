using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Photos.DTOs.Requests;

public sealed record AddAlbumMediaRequest(
    [NonEmptyGuid(ErrorMessage = "Mã ảnh là bắt buộc.")]
    Guid MediaId);
