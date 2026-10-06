using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using Fookbase.Api.Modules.Ai.Config;
using Fookbase.Api.Modules.Ai.DTOs.Requests;
using Fookbase.Api.Modules.Ai.DTOs.Responses;

namespace Fookbase.Api.Modules.Ai.Services;

public sealed class AiChatService(
    IHttpClientFactory httpClientFactory,
    AiChatOptions options,
    ILogger<AiChatService> logger)
{
    public async Task<AiChatServiceResult> ChatAsync(AiChatRequest request, CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            return AiChatServiceResult.Failure(StatusCodes.Status503ServiceUnavailable,
                "Tính năng trò chuyện AI chưa được bật.");
        }

        var input = BuildInput(request.History, request.Message!.Trim());
        var providers = options.GetConfiguredProviders();
        if (providers.Count == 0)
        {
            return AiChatServiceResult.Failure(StatusCodes.Status503ServiceUnavailable,
                "Chưa cấu hình nhà cung cấp AI.");
        }

        foreach (var provider in providers)
        {
            var attempt = await SendAsync(provider, input, cancellationToken);
            if (!string.IsNullOrWhiteSpace(attempt.Content))
            {
                return AiChatServiceResult.Success(new AiChatResponse(attempt.Content, provider.Options.Model));
            }

            if (!attempt.CanFallback)
            {
                return AiChatServiceResult.Failure(StatusCodes.Status502BadGateway,
                    "Dịch vụ AI không thể hoàn thành yêu cầu.");
            }

            logger.LogWarning("AI provider {Provider} was unavailable; trying the next configured provider.", provider.Name);
        }

        return AiChatServiceResult.Failure(StatusCodes.Status503ServiceUnavailable,
            "Các dịch vụ AI hiện tạm thời không khả dụng.");
    }

    private async Task<ProviderAttempt> SendAsync(
        AiChatProvider provider,
        IReadOnlyList<AiInputMessage> input,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(provider.HttpClientName);
            using var response = provider.Protocol switch
            {
                AiChatProviderProtocol.GEMINI => await SendGeminiAsync(client, provider.Options, input, cancellationToken),
                AiChatProviderProtocol.OPEN_AI_RESPONSES => await SendOpenAiResponsesAsync(client, provider.Options, input, cancellationToken),
                _ => await SendOpenAiChatCompletionAsync(client, provider.Options, input, cancellationToken)
            };

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("AI provider {Provider} returned status {StatusCode}.", provider.Name, (int)response.StatusCode);
                return ProviderAttempt.Failure(CanFallback(response.StatusCode));
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
            var content = provider.Protocol switch
            {
                AiChatProviderProtocol.GEMINI => ReadGeminiOutputText(document.RootElement),
                AiChatProviderProtocol.OPEN_AI_RESPONSES => ReadResponsesOutputText(document.RootElement),
                _ => ReadChatCompletionOutputText(document.RootElement)
            };

            if (string.IsNullOrWhiteSpace(content))
            {
                logger.LogWarning("AI provider {Provider} returned no output text.", provider.Name);
                return ProviderAttempt.Failure(canFallback: true);
            }

            return ProviderAttempt.Success(content);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("AI provider {Provider} timed out.", provider.Name);
            return ProviderAttempt.Failure(canFallback: true);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "AI provider {Provider} could not be reached.", provider.Name);
            return ProviderAttempt.Failure(canFallback: true);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "AI provider {Provider} returned an invalid response.", provider.Name);
            return ProviderAttempt.Failure(canFallback: true);
        }
    }

    private Task<HttpResponseMessage> SendOpenAiChatCompletionAsync(
        HttpClient client,
        AiChatProviderOptions provider,
        IReadOnlyList<AiInputMessage> input,
        CancellationToken cancellationToken)
    {
        var messages = new List<AiInputMessage> { new("system", options.Instructions) };
        messages.AddRange(input);
        return client.PostAsJsonAsync("chat/completions", new { model = provider.Model, messages }, cancellationToken);
    }

    private Task<HttpResponseMessage> SendGeminiAsync(
        HttpClient client,
        AiChatProviderOptions provider,
        IReadOnlyList<AiInputMessage> input,
        CancellationToken cancellationToken)
    {
        var contents = input.Select(item => new
        {
            role = item.Role == "assistant" ? "model" : "user",
            parts = new[] { new { text = item.Content } }
        });
        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = options.Instructions } } },
            contents
        };
        return client.PostAsJsonAsync($"v1beta/models/{Uri.EscapeDataString(provider.Model)}:generateContent", body, cancellationToken);
    }

    private Task<HttpResponseMessage> SendOpenAiResponsesAsync(
        HttpClient client,
        AiChatProviderOptions provider,
        IReadOnlyList<AiInputMessage> input,
        CancellationToken cancellationToken) =>
        client.PostAsJsonAsync("v1/responses", new
        {
            model = provider.Model,
            store = false,
            instructions = options.Instructions,
            input
        }, cancellationToken);

    private IReadOnlyList<AiInputMessage> BuildInput(
        IReadOnlyList<AiChatHistoryMessage>? history,
        string message)
    {
        var input = new List<AiInputMessage>();
        foreach (var item in (history ?? []).TakeLast(options.MaximumHistoryMessages))
        {
            var validationContext = new ValidationContext(item);
            validationContext.InitializeServiceProvider(type => type == typeof(AiChatOptions) ? options : null);
            if (!Validator.TryValidateObject(item, validationContext, null, validateAllProperties: true))
            {
                continue;
            }

            input.Add(new AiInputMessage(item.Role!, item.Content!.Trim()));
        }

        input.Add(new AiInputMessage("user", message));
        return input;
    }

    private static bool CanFallback(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private static string? ReadChatCompletionOutputText(JsonElement response)
    {
        if (!response.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var choice in choices.EnumerateArray())
        {
            if (!choice.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object ||
                !message.TryGetProperty("content", out var content))
            {
                continue;
            }

            var text = ReadText(content);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return null;
    }

    private static string? ReadGeminiOutputText(JsonElement response)
    {
        if (!response.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object ||
                !content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var text = parts.EnumerateArray()
                .Select(part => part.GetPropertyOrNull("text"))
                .Where(part => !string.IsNullOrWhiteSpace(part));
            var output = string.Concat(text);
            if (!string.IsNullOrWhiteSpace(output))
            {
                return output;
            }
        }

        return null;
    }

    private static string? ReadResponsesOutputText(JsonElement response)
    {
        if (response.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
        {
            return outputText.GetString();
        }

        if (!response.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var text = new List<string>();
        foreach (var item in output.EnumerateArray())
        {
            if (item.GetPropertyOrNull("type") != "message" ||
                !item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in content.EnumerateArray())
            {
                if (part.GetPropertyOrNull("type") == "output_text" &&
                    part.TryGetProperty("text", out var value) && value.ValueKind == JsonValueKind.String)
                {
                    text.Add(value.GetString()!);
                }
            }
        }

        return text.Count == 0 ? null : string.Concat(text);
    }

    private static string? ReadText(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString();
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return string.Concat(content.EnumerateArray()
            .Where(part => part.GetPropertyOrNull("type") is "text" or "output_text")
            .Select(part => part.GetPropertyOrNull("text"))
            .Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private sealed record AiInputMessage(string Role, string Content);
    private sealed record ProviderAttempt(string? Content, bool CanFallback)
    {
        public static ProviderAttempt Success(string content) => new(content, false);
        public static ProviderAttempt Failure(bool canFallback) => new(null, canFallback);
    }
}

public sealed record AiChatServiceResult(AiChatResponse? Response, int? StatusCode, string? Error)
{
    public bool Succeeded => Response is not null;

    public static AiChatServiceResult Success(AiChatResponse response) => new(response, null, null);

    public static AiChatServiceResult Failure(int statusCode, string error) => new(null, statusCode, error);
}

internal static class JsonElementExtensions
{
    public static string? GetPropertyOrNull(this JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
