namespace Fookbase.Api.Modules.Media.Config;

public sealed class CloudinaryOptions
{
    public const string SectionName = "Cloudinary";

    public string CloudName { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string ApiSecret { get; init; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CloudName) || string.IsNullOrWhiteSpace(ApiKey) ||
            string.IsNullOrWhiteSpace(ApiSecret))
        {
            throw new InvalidOperationException("Cloudinary cloud name, API key and API secret are required.");
        }
    }
}
