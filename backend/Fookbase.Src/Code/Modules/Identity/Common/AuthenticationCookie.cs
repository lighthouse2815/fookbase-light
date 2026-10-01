using Fookbase.Api.Modules.Identity.DTOs.Responses;

namespace Fookbase.Api.Modules.Identity.Common;

public static class AuthenticationCookie
{
    public static bool UsesCookieTransport(HttpRequest request) => GetCookieName(request) is not null;

    public static string? ReadRefreshToken(HttpRequest request, string? bodyToken)
    {
        var cookieName = GetCookieName(request);
        if (cookieName is null) return bodyToken;

        var token = string.IsNullOrWhiteSpace(bodyToken)
            ? request.Cookies[cookieName]
            : bodyToken;
        return token;
    }

    public static void Write(HttpContext context, AuthenticationResponse response)
    {
        if (!UsesCookieTransport(context.Request)) return;

        context.Response.Cookies.Append(GetCookieName(context.Request)!, response.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
            IsEssential = true,
            Expires = response.RefreshTokenExpiresAt
        });
    }

    public static void Clear(HttpContext context)
    {
        if (!UsesCookieTransport(context.Request)) return;
        context.Response.Cookies.Delete(GetCookieName(context.Request)!, new CookieOptions { Path = "/api/auth" });
    }

    public static object Present(HttpContext context, AuthenticationResponse response)
    {
        Write(context, response);
        return UsesCookieTransport(context.Request)
            ? new BrowserAuthenticationResponse(
                response.User,
                response.AccessToken,
                response.AccessTokenExpiresAt,
                response.RefreshTokenExpiresAt)
            : response;
    }

    public static object Present(HttpContext context, object result) =>
        result is AuthenticationResponse response ? Present(context, response) : result;

    private static string? GetCookieName(HttpRequest request) =>
        request.Headers[IdentityModuleConstants.AuthenticationCookie.TransportHeader].ToString().ToLowerInvariant() switch
        {
            IdentityModuleConstants.AuthenticationCookie.WebTransport => IdentityModuleConstants.AuthenticationCookie.WebCookieName,
            IdentityModuleConstants.AuthenticationCookie.AdminTransport => IdentityModuleConstants.AuthenticationCookie.AdminCookieName,
            IdentityModuleConstants.AuthenticationCookie.ZolaLightTransport => IdentityModuleConstants.AuthenticationCookie.ZolaLightCookieName,
            _ => null
        };
}
