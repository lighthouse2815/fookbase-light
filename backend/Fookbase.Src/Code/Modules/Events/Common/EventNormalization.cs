using Fookbase.Api.Modules.Events.Entities;
using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Events.Common;

public static class EventNormalization
{
    public static string NormalizeName(string? value)
    {
        var text = TextNormalization.NormalizeOptionalText(value) ?? string.Empty;
        if (text.Length is < 1 or > Event.MaximumNameLength)
            throw new ArgumentException($"Event name must contain 1-{Event.MaximumNameLength} characters.");
        return text;
    }

    public static string? NormalizeDescription(string? value)
    {
        var text = TextNormalization.NormalizeOptionalText(value);
        if (text?.Length > Event.MaximumDescriptionLength)
            throw new ArgumentException($"Event description cannot exceed {Event.MaximumDescriptionLength} characters.");
        return text;
    }

    public static DateTimeOffset? NormalizeEnd(DateTimeOffset? end, DateTimeOffset start)
    {
        if (end is null)
            return null;
        var utc = end.Value.ToUniversalTime();
        if (utc <= start)
            throw new ArgumentException("Event end time must be after start time.");
        return utc;
    }
}
