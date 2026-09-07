namespace Fookbase.Media.Api.Authentication;

public sealed class InternalServicesOptions
{
    public const string SectionName = "InternalServices";
    public string Token { get; init; } = string.Empty;
    public void Validate()
    {
        if (Token.Length < 32)
            throw new InvalidOperationException("Internal service token must contain at least 32 characters.");
    }
}
