namespace Fookbase.Media.Api.Authentication;

public sealed class JwtValidationOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string SigningKey { get; init; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer))
        {
            throw new InvalidOperationException("JWT issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException("JWT audience is required.");
        }

        if (SigningKey.Length < 32)
        {
            throw new InvalidOperationException("JWT signing key must contain at least 32 characters.");
        }
    }
}
