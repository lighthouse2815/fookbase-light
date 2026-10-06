using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Stories.Common;

public static class StoryNormalization
{
    public static string? NormalizeCaption(string? caption)
    {
        var normalized = TextNormalization.NormalizeOptionalText(caption);
        if (normalized?.Length > 2_200)
        {
            throw new ArgumentException("Story caption cannot exceed 2200 characters.");
        }

        return normalized;
    }
}
