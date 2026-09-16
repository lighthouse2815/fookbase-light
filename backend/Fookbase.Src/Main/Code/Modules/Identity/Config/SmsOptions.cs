namespace Fookbase.Api.Modules.Identity.Config;

public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    public bool Enabled { get; init; }

    public string AccessToken { get; init; } = string.Empty;

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

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new InvalidOperationException("Sms:BaseUrl must be an absolute HTTPS URL when SMS is enabled.");
        }
    }
}
