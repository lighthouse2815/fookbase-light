using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Config;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed class TraccarSmsSender(HttpClient client, SmsOptions options, ILogger<TraccarSmsSender> logger)
{
    public async Task SendOtpAsync(
        string phoneNumber,
        string code,
        CancellationToken cancellationToken = default)
    {
        if (!options.Enabled || options.Provider != IdentityModuleConstants.SmsProviders.Traccar)
        {
            throw new InvalidOperationException("SMS delivery is not configured.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, string.Empty)
        {
            Content = JsonContent.Create(new
            {
                to = phoneNumber,
                message = $"Ma xac nhan Fookbase cua ban la: {code}"
            })
        };
        request.Headers.TryAddWithoutValidation("Authorization", options.AccessToken);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Traccar rejected OTP dispatch with HTTP status {StatusCode}.", (int)response.StatusCode);
            throw new InvalidOperationException("The SMS provider could not deliver the verification code.");
        }
    }
}
