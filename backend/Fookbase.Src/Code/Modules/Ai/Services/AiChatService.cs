using System.Net.Http.Json;
using System.Text.Json;
using Fookbase.Api.Modules.Ai.Config;
using Fookbase.Api.Modules.Ai.DTOs.Requests;
using Fookbase.Api.Modules.Ai.DTOs.Responses;

namespace Fookbase.Api.Modules.Ai.Services;

public sealed class AiChatService(
    HttpClient httpClient,
    AiChatOptions options,
    ILogger<AiChatService> logger)
{
    public async Task<AiChatServiceResult> ChatAsync(AiChatRequest request, CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            return AiChatServiceResult.Failure(StatusCodes.Status503ServiceUnavailable,
                "AI chat is not enabled.");
        }

        var message = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return AiChatServiceResult.Failure(StatusCodes.Status400BadRequest, "Message is required.");
        }

        if (message.Length > options.MaximumInputCharacters)
        {
            return AiChatServiceResult.Failure(StatusCodes.Status400BadRequest,
                $"Message must not exceed {options.MaximumInputCharacters} characters.");
        }

        var input = BuildInput(request.History, message);
        try
        {
            using var response = await httpClient.PostAsJsonAsync("v1/responses", new
            {
                model = options.Model,
                store = false,
                instructions = options.Instructions,
                input
            }, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OpenAI Responses API returned status {StatusCode}.", (int)response.StatusCode);
                return AiChatServiceResult.Failure(StatusCodes.Status502BadGateway,
                    "The AI service could not complete the request.");
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
            var content = ReadOutputText(document.RootElement);
            if (string.IsNullOrWhiteSpace(content))
            {
                logger.LogWarning("OpenAI Responses API returned no output text.");
                return AiChatServiceResult.Failure(StatusCodes.Status502BadGateway,
                    "The AI service returned an empty response.");
            }

            return AiChatServiceResult.Success(new AiChatResponse(content, options.Model));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("OpenAI Responses API timed out.");
            return AiChatServiceResult.Failure(StatusCodes.Status504GatewayTimeout,
                "The AI service took too long to respond.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "OpenAI Responses API could not be reached.");
            return AiChatServiceResult.Failure(StatusCodes.Status502BadGateway,
                "The AI service is temporarily unavailable.");
        }
    }

    private IReadOnlyList<AiInputMessage> BuildInput(
        IReadOnlyList<AiChatHistoryMessage>? history,
        string message)
    {
        var input = new List<AiInputMessage>();
        foreach (var item in (history ?? []).TakeLast(options.MaximumHistoryMessages))
        {
            var content = item.Content?.Trim();
            if (string.IsNullOrWhiteSpace(content) || content.Length > options.MaximumInputCharacters ||
                item.Role is not ("user" or "assistant"))
            {
                continue;
            }

            input.Add(new AiInputMessage(item.Role, content));
        }

        input.Add(new AiInputMessage("user", message));
        return input;
    }

    private static string? ReadOutputText(JsonElement response)
    {
        if (response.TryGetProperty("output_text", out var outputText) &&
            outputText.ValueKind == JsonValueKind.String)
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

    private sealed record AiInputMessage(string Role, string Content);
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
