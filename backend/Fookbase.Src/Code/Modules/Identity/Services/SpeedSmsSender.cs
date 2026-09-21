using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Fookbase.Api.Modules.Identity.Config;
using Microsoft.Extensions.Logging;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed class SpeedSmsSender(HttpClient client, SmsOptions options, ILogger<SpeedSmsSender> logger)
{
    public async Task<string> SendOtpAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled) throw new InvalidOperationException("SMS delivery is not configured.");
        var recipient = phoneNumber.StartsWith("+84", StringComparison.Ordinal) ? $"0{phoneNumber[3..]}" : phoneNumber;
        using var request = new HttpRequestMessage(HttpMethod.Post, "index.php/pin/create")
        {
            Content = JsonContent.Create(new
            {
                to = recipient,
                content = "Ma xac nhan Fookbase cua ban la: {pin_code}",
                app_id = options.TwoFactorApplicationId
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{options.AccessToken}:x")));
        using var response = await client.SendAsync(request, cancellationToken);
        var result = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<Response>(cancellationToken: cancellationToken) : null;
        if (result?.Status is not "success" || result.Code is not "00" || !IsOtp(result.Data?.PinCode))
        {
            logger.LogWarning("SpeedSMS rejected OTP dispatch with HTTP status {StatusCode}, provider code {ProviderCode}, and message {ProviderMessage}.",
                (int)response.StatusCode, result?.Code ?? "unknown", RedactDigits(result?.Message));
            throw new InvalidOperationException("The SMS provider could not deliver the verification code.");
        }

        return result.Data!.PinCode!;
    }

    private static bool IsOtp(string? value) => value is { Length: 6 } && value.All(char.IsAsciiDigit);

    private static string RedactDigits(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : Regex.Replace(value, @"\d{3,}", "[redacted]");

    private sealed record Response(string? Status, string? Code, string? Message, ResponseData? Data);

    private sealed record ResponseData([property: JsonPropertyName("pin_code")] string? PinCode);
}
