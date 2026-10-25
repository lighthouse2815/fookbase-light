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
            throw new InvalidOperationException("Phải cấu hình tên ứng dụng cho Data Protection.");
        }

        if (persistentKeyRingRequired && string.IsNullOrWhiteSpace(KeyRingPath))
        {
            throw new InvalidOperationException(
                "Phải cấu hình DataProtection:KeyRingPath khi chạy trong môi trường Production.");
        }
    }
}
