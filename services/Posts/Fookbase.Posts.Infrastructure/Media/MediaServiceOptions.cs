namespace Fookbase.Posts.Infrastructure.Media;

public sealed class MediaServiceOptions
{
    public const string SectionName = "MediaService";
    public string BaseUrl { get; init; } = "http://localhost:5005";
    public string InternalToken { get; init; } = string.Empty;
    public void Validate()
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _) || InternalToken.Length < 32)
            throw new InvalidOperationException("A valid Media base URL and internal token are required.");
    }
}
