namespace Fookbase.Api.Modules.Ai.Config;

public sealed class AiChatOptions
{
    public const string SectionName = "AiChat";

    public bool Enabled { get; init; }
    public string ApiKey { get; init; } = string.Empty;
    public string ApiBaseUrl { get; init; } = "https://api.openai.com/";
    public string Model { get; init; } = "gpt-5-mini";
    public string Instructions { get; init; } =
        "Bạn là trợ lý AI của Fookbase. Trả lời hữu ích, rõ ràng và bằng ngôn ngữ người dùng sử dụng.";
    public int RequestTimeoutSeconds { get; init; } = 45;
    public int MaximumInputCharacters { get; init; } = 4_000;
    public int MaximumHistoryMessages { get; init; } = 10;

    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("AiChat:ApiKey is required when AI chat is enabled.");
        }

        if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(baseUri.Host))
        {
            throw new InvalidOperationException("AiChat:ApiBaseUrl must be an absolute HTTPS URL when AI chat is enabled.");
        }

        if (string.IsNullOrWhiteSpace(Model) || string.IsNullOrWhiteSpace(Instructions) ||
            RequestTimeoutSeconds is < 1 or > 120 ||
            MaximumInputCharacters is < 1 or > 20_000 ||
            MaximumHistoryMessages is < 0 or > 30)
        {
            throw new InvalidOperationException("AI chat options are outside the supported bounds.");
        }
    }
}
