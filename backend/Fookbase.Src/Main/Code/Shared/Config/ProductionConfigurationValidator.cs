using Fookbase.Api.Modules.Identity.Config;
using Fookbase.Api.Modules.Media.Config;
using Microsoft.Extensions.Configuration;

namespace Fookbase.Api.Shared.Config;

public static class ProductionConfigurationValidator
{
    public static void Validate(IConfiguration configuration, bool production)
    {
        if (!production)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("FookbaseDatabase")))
        {
            throw new InvalidOperationException("ConnectionStrings:FookbaseDatabase is required in Production.");
        }

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is required in Production.");
        jwt.Validate();

        var googleAuthentication = configuration
            .GetSection(GoogleAuthenticationOptions.SectionName)
            .Get<GoogleAuthenticationOptions>() ?? new GoogleAuthenticationOptions();
        googleAuthentication.Validate(production: true);

        var sms = configuration.GetSection(SmsOptions.SectionName).Get<SmsOptions>()
            ?? new SmsOptions();
        sms.Validate(production: true);

        var cloudinary = configuration.GetSection(CloudinaryOptions.SectionName).Get<CloudinaryOptions>()
            ?? throw new InvalidOperationException("Cloudinary configuration is required in Production.");
        cloudinary.Validate();

        var dataProtection = configuration.GetSection(DataProtectionOptions.SectionName)
            .Get<DataProtectionOptions>() ?? new DataProtectionOptions();
        dataProtection.Validate(persistentKeyRingRequired: true);

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length == 0 || origins.Any(origin =>
                !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                string.IsNullOrWhiteSpace(uri.Host)))
        {
            throw new InvalidOperationException(
                "Cors:AllowedOrigins must contain explicit HTTPS origins in Production.");
        }

        var allowedHosts = configuration["AllowedHosts"];
        if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts == "*")
        {
            throw new InvalidOperationException("AllowedHosts must list explicit API hosts in Production.");
        }

        var forwardedHeaders = configuration.GetSection(ForwardedHeadersOptions.SectionName)
            .Get<ForwardedHeadersOptions>() ?? new ForwardedHeadersOptions();
        forwardedHeaders.Validate(production: true);
    }
}
