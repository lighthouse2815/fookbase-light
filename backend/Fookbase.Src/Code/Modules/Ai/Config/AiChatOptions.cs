namespace Fookbase.Api.Modules.Ai.Config;

public sealed class AiChatOptions
{
    public const string SectionName = "AiChat";

    public bool Enabled { get; init; }

    public AiChatProviderOptions Groq { get; init; } = new()
    {
        ApiBaseUrl = "https://api.groq.com/openai/v1/",
        Model = "openai/gpt-oss-120b"
    };

    public AiChatProviderOptions Gemini { get; init; } = new()
    {
        ApiBaseUrl = "https://generativelanguage.googleapis.com/",
        Model = "gemini-2.5-flash-lite"
    };

    public AiChatProviderOptions OpenRouter { get; init; } = new()
    {
        ApiBaseUrl = "https://openrouter.ai/api/v1/",
        Model = "openrouter/free"
    };

    // Keep the original OpenAI configuration working when no provider in the preferred chain is configured.
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

        if (string.IsNullOrWhiteSpace(Instructions) ||
            RequestTimeoutSeconds is < 1 or > 120 ||
            MaximumInputCharacters is < 1 or > 20_000 ||
            MaximumHistoryMessages is < 0 or > 30)
        {
            throw new InvalidOperationException("AI chat options are outside the supported bounds.");
        }

        var providers = GetConfiguredProviders();
        if (providers.Count == 0)
        {
            throw new InvalidOperationException(
                "Configure at least one API key: AiChat:Groq:ApiKey, AiChat:Gemini:ApiKey, or AiChat:OpenRouter:ApiKey.");
        }

        foreach (var provider in providers)
        {
            provider.Options.Validate();
        }
    }

    internal IReadOnlyList<AiChatProvider> GetConfiguredProviders()
    {
        var providers = new List<AiChatProvider>();
        AddProvider(providers, "Groq", "ai-chat-groq", AiChatProviderProtocol.OpenAiChatCompletions, Groq);
        AddProvider(providers, "Gemini", "ai-chat-gemini", AiChatProviderProtocol.Gemini, Gemini);
        AddProvider(providers, "OpenRouter", "ai-chat-openrouter", AiChatProviderProtocol.OpenAiChatCompletions, OpenRouter);

        if (providers.Count == 0 && !string.IsNullOrWhiteSpace(ApiKey))
        {
            AddProvider(providers, "OpenAI", "ai-chat-openai", AiChatProviderProtocol.OpenAiResponses,
                new AiChatProviderOptions { ApiKey = ApiKey, ApiBaseUrl = ApiBaseUrl, Model = Model });
        }

        return providers;
    }

    private static void AddProvider(
        ICollection<AiChatProvider> providers,
        string name,
        string httpClientName,
        AiChatProviderProtocol protocol,
        AiChatProviderOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            providers.Add(new AiChatProvider(name, httpClientName, protocol, options));
        }
    }
}

public sealed class AiChatProviderOptions
{
    public string ApiKey { get; init; } = string.Empty;
    public string ApiBaseUrl { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey) || string.IsNullOrWhiteSpace(Model) ||
            !Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(baseUri.Host))
        {
            throw new InvalidOperationException("Configured AI providers require an API key, model, and absolute HTTPS base URL.");
        }
    }
}

internal sealed record AiChatProvider(
    string Name,
    string HttpClientName,
    AiChatProviderProtocol Protocol,
    AiChatProviderOptions Options);

internal enum AiChatProviderProtocol
{
    OpenAiChatCompletions,
    Gemini,
    OpenAiResponses
}
