using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Photos.Entities;

namespace Fookbase.Api.Modules.Photos.DTOs.Requests;

public sealed record UpdateAlbumMediaRequest(
    [TrimmedStringLength(AlbumMedia.MaximumCaptionLength, ErrorMessage = "Chú thích ảnh không được vượt quá {1} ký tự.")]
    string? Caption);
