using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Messages.Common;

public static class ConversationNormalization
{
    public static string NormalizeTitle(string? value)
    {
        var normalized = TextNormalization.NormalizeOptionalText(value) ?? string.Empty;
        if (normalized.Length is < 1 or > 120)
        {
            throw new ArgumentException("Group conversation title must contain between 1 and 120 characters.");
        }

        return normalized;
    }
}
