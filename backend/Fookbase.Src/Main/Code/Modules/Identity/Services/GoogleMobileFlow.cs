using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed class GoogleMobileFlow(IDataProtectionProvider provider)
{
    private readonly ITimeLimitedDataProtector protector = provider.CreateProtector("Fookbase.GoogleMobile.Completion.v1").ToTimeLimitedDataProtector();
    private sealed record Payload(string Code, string Challenge);
    public static bool IsValidChallenge(string? value) => value is not null && Regex.IsMatch(value, "^[A-Za-z0-9_-]{43}$");
    public static bool IsValidState(string? value) => value is not null && Regex.IsMatch(value, "^[A-Za-z0-9_-]{43,128}$");
    public string Protect(string code, string challenge)
    {
        if (!IsValidChallenge(challenge)) throw new ArgumentException("Invalid PKCE challenge.", nameof(challenge));
        return protector.Protect(JsonSerializer.Serialize(new Payload(code, challenge)), TimeSpan.FromMinutes(5));
    }
    public string? Unprotect(string? token, string? verifier)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096 || verifier is null || !Regex.IsMatch(verifier, "^[A-Za-z0-9._~-]{43,128}$")) return null;
        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(protector.Unprotect(token));
            if (payload is null || !IsValidChallenge(payload.Challenge)) return null;
            var actual = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
            return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actual), Encoding.ASCII.GetBytes(payload.Challenge)) ? payload.Code : null;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or ArgumentException)
        {
            return null;
        }
    }
}
