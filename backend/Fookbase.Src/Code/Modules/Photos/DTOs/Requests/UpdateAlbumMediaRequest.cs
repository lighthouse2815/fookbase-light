using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Photos.DTOs.Requests;

public sealed record UpdateAlbumMediaRequest(
    [TrimmedStringLength(1_000, ErrorMessage = "Chú thích ảnh không được vượt quá {1} ký tự.")]
    string? Caption);
