using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;

namespace Fookbase.Api.Shared.Observability;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = RequestCorrelation.GetId(context);
        context.Response.Headers[RequestCorrelation.HeaderName] = requestId;
        var stopwatch = Stopwatch.StartNew();

        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["RequestId"] = requestId,
            ["UserId"] = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        }))
        {
            try
            {
                await next(context);
            }
            finally
            {
                logger.LogInformation(
                    "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds} ms.",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    stopwatch.Elapsed.TotalMilliseconds);
            }
        }
    }
}
