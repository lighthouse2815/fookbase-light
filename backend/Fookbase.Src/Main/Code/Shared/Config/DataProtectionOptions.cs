namespace Fookbase.Api.Shared.Config;

public sealed class DataProtectionOptions
{
    public const string SectionName = "DataProtection";

    public string ApplicationName { get; init; } = "Fookbase";

    public string? KeyRingPath { get; init; }

    public void Validate(bool persistentKeyRingRequired)
    {
        if (string.IsNullOrWhiteSpace(ApplicationName))
        {
            throw new InvalidOperationException("The Data Protection application name is required.");
        }

        if (persistentKeyRingRequired && string.IsNullOrWhiteSpace(KeyRingPath))
        {
            throw new InvalidOperationException(
                "DataProtection:KeyRingPath is required when running in Production.");
        }
    }
}
