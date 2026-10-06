using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Posts.Common;

public static class PostShareNormalization
{
    public static string? NormalizeCaption(string? caption)
    {
        var normalized = TextNormalization.NormalizeOptionalText(caption);
        if (normalized?.Length > 10_000)
        {
            throw new ArgumentException("Share caption cannot exceed 10000 characters.");
        }

        return normalized;
    }
}
