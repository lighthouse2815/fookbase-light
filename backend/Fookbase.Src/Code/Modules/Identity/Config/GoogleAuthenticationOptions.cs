namespace Fookbase.Api.Modules.Identity.Config;

public sealed class GoogleAuthenticationOptions
{
    public const string SectionName = "GoogleAuthentication";

    public bool Enabled { get; init; }

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string WebBaseUrl { get; init; } = string.Empty;

    public string ZolaLightBaseUrl { get; init; } = string.Empty;
    public string MobileCallbackUrl { get; init; } = string.Empty;
    public string ZolaMobileCallbackUrl { get; init; } = string.Empty;

    public void Validate(bool production)
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ClientId))
        {
            throw new InvalidOperationException("GoogleAuthentication:ClientId is required when Google authentication is enabled.");
        }

        if (string.IsNullOrWhiteSpace(ClientSecret))
        {
            throw new InvalidOperationException("GoogleAuthentication:ClientSecret is required when Google authentication is enabled.");
        }

        ValidateClientUrl(WebBaseUrl, "GoogleAuthentication:WebBaseUrl", production);
        ValidateClientUrl(ZolaLightBaseUrl, "GoogleAuthentication:ZolaLightBaseUrl", production);
        ValidateMobileCallbackUrl(MobileCallbackUrl, "GoogleAuthentication:MobileCallbackUrl");
        ValidateMobileCallbackUrl(ZolaMobileCallbackUrl, "GoogleAuthentication:ZolaMobileCallbackUrl");
    }

    public string GetClientLoginUri(string client) => client switch
    {
        "web" => BuildLoginUri(WebBaseUrl, "GoogleAuthentication:WebBaseUrl"),
        "zola-light" => BuildLoginUri(ZolaLightBaseUrl, "GoogleAuthentication:ZolaLightBaseUrl"),
        _ => throw new InvalidOperationException("The Google authentication client is unsupported.")
    };

    public string GetMobileCallbackUrl(string client) => client switch
    {
        "mobile" when !string.IsNullOrWhiteSpace(MobileCallbackUrl) => MobileCallbackUrl,
        "zola-mobile" when !string.IsNullOrWhiteSpace(ZolaMobileCallbackUrl) => ZolaMobileCallbackUrl,
        _ => throw new InvalidOperationException("The Google mobile client is unsupported or has no callback URL.")
    };

    public bool IsMobileClientEnabled(string client) => client switch
    {
        "mobile" => !string.IsNullOrWhiteSpace(MobileCallbackUrl),
        "zola-mobile" => !string.IsNullOrWhiteSpace(ZolaMobileCallbackUrl),
        _ => false
    };

    private static void ValidateClientUrl(string value, string name, bool production)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new InvalidOperationException($"{name} must be an absolute URL when Google authentication is enabled.");
        }

        if (production && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"{name} must use HTTPS in Production.");
        }
    }

    private static void ValidateMobileCallbackUrl(string value, string name)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            (!Uri.TryCreate(value, UriKind.Absolute, out var callback) ||
             callback.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(callback.Host) ||
             !string.IsNullOrEmpty(callback.UserInfo) || !string.IsNullOrEmpty(callback.Query) || !string.IsNullOrEmpty(callback.Fragment)))
        {
            throw new InvalidOperationException($"{name} must be an explicit HTTPS callback without query, fragment or credentials.");
        }
    }

    private static string BuildLoginUri(string value, string name)
    {
        ValidateClientUrl(value, name, production: false);
        return $"{value.TrimEnd('/')}/login";
    }
}
