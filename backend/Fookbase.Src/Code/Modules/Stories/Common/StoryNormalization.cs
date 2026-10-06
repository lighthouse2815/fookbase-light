using Fookbase.Api.Modules.Stories.Entities;
using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Stories.Common;

public static class StoryNormalization
{
    public static string? NormalizeCaption(string? caption)
    {
        var normalized = TextNormalization.NormalizeOptionalText(caption);
        if (normalized?.Length > Story.MaximumCaptionLength)
        {
            throw new ArgumentException($"Story caption cannot exceed {Story.MaximumCaptionLength} characters.");
        }

        return normalized;
    }
}
