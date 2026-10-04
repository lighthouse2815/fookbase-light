using Fookbase.Api.Modules.Media.Domain.Enums;

namespace Fookbase.Api.Modules.Media.Common;

internal sealed record MediaFormat(MediaType MediaType, string Extension)
{
    public static readonly IReadOnlyDictionary<string, MediaFormat> SupportedFormats =
        new Dictionary<string, MediaFormat>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = new(MediaType.IMAGE, ".jpg"),
            ["image/png"] = new(MediaType.IMAGE, ".png"),
            ["image/webp"] = new(MediaType.IMAGE, ".webp"),
            ["video/mp4"] = new(MediaType.VIDEO, ".mp4"),
            ["video/webm"] = new(MediaType.VIDEO, ".webm")
        };
}
