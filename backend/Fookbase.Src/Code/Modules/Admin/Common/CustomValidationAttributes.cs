using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Posts.Domain.Enums;

namespace Fookbase.Api.Modules.Admin.Common;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class ReportStatusUpdateAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is null ||
        value is string text &&
        Enum.TryParse<ContentReportStatus>(text, true, out var status) &&
        Enum.IsDefined(status) && status != ContentReportStatus.PENDING;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class ValidModerationCursorAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is null) return true;
        if (value is not string text) return false;

        try
        {
            _ = ModerationCursor.DecodeOrNull(text);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
