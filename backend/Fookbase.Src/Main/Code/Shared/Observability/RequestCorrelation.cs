using System.Diagnostics;

namespace Fookbase.Api.Shared.Observability;

public static class RequestCorrelation
{
    public const string HeaderName = "X-Request-Id";

    public static string GetId(HttpContext context) =>
        Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
}
