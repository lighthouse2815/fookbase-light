using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Identity.Config;

public sealed class SmsOptions
{
    public bool Enabled { get; init; }

    public string Provider { get; init; } = IdentityModuleConstants.SmsProviders.SpeedSms;

    public string AccessToken { get; init; } = string.Empty;

    public string TwoFactorApplicationId { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = "https://api.speedsms.vn";

    public void Validate(bool production)
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(AccessToken))
        {
            throw new InvalidOperationException("Sms:AccessToken is required when SMS is enabled.");
        }

        if (Provider is not IdentityModuleConstants.SmsProviders.SpeedSms and not IdentityModuleConstants.SmsProviders.Traccar)
        {
            throw new InvalidOperationException("Sms:Provider must be either SpeedSms or Traccar when SMS is enabled.");
        }

        if (Provider == IdentityModuleConstants.SmsProviders.SpeedSms && string.IsNullOrWhiteSpace(TwoFactorApplicationId))
        {
            throw new InvalidOperationException("Sms:TwoFactorApplicationId is required when Sms:Provider is SpeedSms.");
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new InvalidOperationException("Sms:BaseUrl must be an absolute HTTPS URL when SMS is enabled.");
        }
    }
}
