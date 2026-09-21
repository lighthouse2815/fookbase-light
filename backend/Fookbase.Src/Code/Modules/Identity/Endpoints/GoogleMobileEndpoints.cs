using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Config;
using Fookbase.Api.Modules.Identity.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.WebUtilities;

namespace Fookbase.Api.Modules.Identity.Endpoints;

public static class GoogleMobileEndpoints
{
    public sealed record CompletionRequest(string? Code, string? Verifier, string? Password = null);
    public static void MapGoogleMobileEndpoints(this RouteGroupBuilder auth)
    {
        var group = auth.MapGroup("/google/mobile").AllowAnonymous().RequireRateLimiting("auth-login");
        group.MapGet("/start", Start);
        group.MapGet("/callback", Callback);
        group.MapPost("/exchange", Exchange);
        group.MapPost("/link", Link);
    }
    private static bool Enabled(GoogleAuthenticationOptions options) => options.Enabled && !string.IsNullOrWhiteSpace(options.MobileCallbackUrl);
    private static IResult Invalid() => new ApplicationError("invalid_mobile_google_login", "The mobile Google sign-in is invalid or expired.", ApplicationErrorType.Unauthorized).ToHttpResult();
    private static IResult Start(string? challenge, string? state, GoogleAuthenticationOptions options)
    {
        if (!Enabled(options)) return Results.NotFound();
        if (!GoogleMobileFlow.IsValidChallenge(challenge) || !GoogleMobileFlow.IsValidState(state)) return Invalid();
        var properties = new AuthenticationProperties { RedirectUri = "/api/auth/google/mobile/callback" };
        properties.Items["fookbase.client"] = "mobile";
        properties.Items["fookbase.code_challenge"] = challenge;
        properties.Items["fookbase.state"] = state;
        return Results.Challenge(properties, ["Google"]);
    }
    private static async Task<IResult> Callback(HttpContext context, GoogleAuthenticationOptions options,
        IGoogleExternalIdentityReader reader, GoogleAuthenticationService service, GoogleMobileFlow flow, CancellationToken cancellationToken)
    {
        if (!Enabled(options)) return Results.NotFound();
        var identity = await reader.ReadAsync(context, cancellationToken);
        if (!identity.Succeeded || identity.Value!.Client != "mobile" ||
            !GoogleMobileFlow.IsValidChallenge(identity.Value.CodeChallenge) || !GoogleMobileFlow.IsValidState(identity.Value.State)) return Invalid();
        var completion = await service.CreateCompletionAsync("mobile", identity.Value.ProviderKey, identity.Value.Email, identity.Value.EmailVerified, cancellationToken);
        if (!completion.Succeeded) return completion.Error!.ToHttpResult();
        return Results.Redirect(QueryHelpers.AddQueryString(options.MobileCallbackUrl, new Dictionary<string, string?>
        {
            ["code"] = flow.Protect(completion.Value!.Code, identity.Value.CodeChallenge!),
            ["state"] = identity.Value.State,
            ["mode"] = completion.Value.RequiresPassword ? "link" : "login"
        }));
    }
    private static async Task<IResult> Exchange(CompletionRequest request, HttpContext context, GoogleAuthenticationOptions options,
        GoogleMobileFlow flow, GoogleAuthenticationService service, CancellationToken cancellationToken)
    {
        if (!Enabled(options)) return Results.NotFound();
        var code = flow.Unprotect(request.Code, request.Verifier);
        if (code is null) return Invalid();
        var result = await service.ExchangeAsync(code, "mobile", context.Request.Headers.UserAgent.ToString(), cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }
    private static async Task<IResult> Link(CompletionRequest request, HttpContext context, GoogleAuthenticationOptions options,
        GoogleMobileFlow flow, GoogleAuthenticationService service, CancellationToken cancellationToken)
    {
        if (!Enabled(options)) return Results.NotFound();
        var code = flow.Unprotect(request.Code, request.Verifier);
        if (code is null) return Invalid();
        var result = await service.LinkExistingAsync(code, "mobile", request.Password ?? string.Empty, context.Request.Headers.UserAgent.ToString(), cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }
}
