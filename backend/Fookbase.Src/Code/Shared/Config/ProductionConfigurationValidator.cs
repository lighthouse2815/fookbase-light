using Fookbase.Api.Modules.Identity.Config;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Media.Config;

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
            throw new InvalidOperationException("Phải cấu hình ConnectionStrings:FookbaseDatabase trong môi trường Production.");
        }

        var jwt = configuration.GetSection(IdentityModuleConstants.ConfigurationSections.Jwt).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Phải cấu hình JWT trong môi trường Production.");
        jwt.Validate();

        var googleAuthentication = configuration
            .GetSection(IdentityModuleConstants.ConfigurationSections.GoogleAuthentication)
            .Get<GoogleAuthenticationOptions>() ?? new GoogleAuthenticationOptions();
        googleAuthentication.Validate(production: true);

        var sms = configuration.GetSection(IdentityModuleConstants.ConfigurationSections.Sms).Get<SmsOptions>()
            ?? new SmsOptions();
        sms.Validate(production: true);

        var cloudinary = configuration.GetSection(CloudinaryOptions.SectionName).Get<CloudinaryOptions>()
            ?? throw new InvalidOperationException("Phải cấu hình Cloudinary trong môi trường Production.");
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
                "Cors:AllowedOrigins phải chứa danh sách origin HTTPS cụ thể trong môi trường Production.");
        }

        var allowedHosts = configuration["AllowedHosts"];
        if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts == "*")
        {
            throw new InvalidOperationException("AllowedHosts phải liệt kê các tên host API cụ thể trong môi trường Production.");
        }

        var forwardedHeaders = configuration.GetSection(ForwardedHeadersOptions.SectionName)
            .Get<ForwardedHeadersOptions>() ?? new ForwardedHeadersOptions();
        forwardedHeaders.Validate(production: true);
    }
}
