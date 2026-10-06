using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Media.Common;

namespace Fookbase.Api.Modules.Media.DTOs.Requests;

public sealed record CreateUploadRequest(
    [Required]
    [ValidMediaFileName]
    string FileName,

    [Required]
    [SupportedMediaContentType(ErrorMessage = "Định dạng hỗ trợ: JPEG, PNG, WebP, MP4 và WebM.")]
    string ContentType,

    [Range(typeof(long), "1", "9223372036854775807")]
    [MediaUploadSizeLimit]
    long SizeBytes);
