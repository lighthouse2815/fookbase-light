namespace Fookbase.Api.Shared.Security;

public sealed class SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
            context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
            context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
            context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), geolocation=(), microphone=()");
            if (environment.IsProduction())
            {
                context.Response.Headers.TryAdd(
                    "Strict-Transport-Security",
                    "max-age=31536000; includeSubDomains");
            }

            return Task.CompletedTask;
        });

        await next(context);
    }
}
