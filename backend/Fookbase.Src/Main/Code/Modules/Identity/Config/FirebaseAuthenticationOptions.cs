namespace Fookbase.Api.Modules.Identity.Config;

public sealed class FirebaseAuthenticationOptions
{
    public const string SectionName = "FirebaseAuthentication";

    public bool Enabled { get; init; }

    public string ProjectId { get; init; } = string.Empty;

    public string ServiceAccountJson { get; init; } = string.Empty;

    public void Validate(bool production)
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ProjectId))
        {
            throw new InvalidOperationException("FirebaseAuthentication:ProjectId is required when Firebase phone authentication is enabled.");
        }

        if (production && string.IsNullOrWhiteSpace(ServiceAccountJson))
        {
            throw new InvalidOperationException("FirebaseAuthentication:ServiceAccountJson is required in Production when Firebase phone authentication is enabled.");
        }
    }
}
