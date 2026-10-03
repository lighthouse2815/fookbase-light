namespace Fookbase.Api.Modules.Identity.Common;

public static class IdentityModuleConstants
{
    public static class Roles
    {
        public const string Admin = "Admin";
    }

    public static class ExternalLogin
    {
        public const string GoogleScheme = "Google";
        public const string ExternalScheme = "GoogleExternal";

        public static class Clients
        {
            public const string Web = "web";
            public const string ZolaLight = "zola-light";
            public const string Mobile = "mobile";
            public const string ZolaMobile = "zola-mobile";
        }
    }

    public static class ConfigurationSections
    {
        public const string Jwt = "Jwt";
        public const string GoogleAuthentication = "GoogleAuthentication";
        public const string Email = "Email";
        public const string Sms = "Sms";
    }

    public static class SmsProviders
    {
        public const string SpeedSms = "SpeedSms";
        public const string Traccar = "Traccar";
    }

    public static class AuthenticationCookie
    {
        public const string TransportHeader = "X-Fookbase-Auth-Transport";
        public const string WebTransport = "cookie:web";
        public const string AdminTransport = "cookie:admin";
        public const string ZolaLightTransport = "cookie:zola-light";
        public const string WebCookieName = "fookbase.web.refresh";
        public const string AdminCookieName = "fookbase.admin.refresh";
        public const string ZolaLightCookieName = "fookbase.zola-light.refresh";
    }

    public static class Challenges
    {
        public const int MaximumFailedAttempts = 5;
        public const int MaximumSendsPerWindow = 5;
        public const int MinimumRegistrationAge = 13;
    }

    public static class Administration
    {
        public const int MaximumPageSize = 100;
    }
}
