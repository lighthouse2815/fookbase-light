using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Fookbase.Api.Modules.Ai;
using Fookbase.Api.Modules.Ai.Config;
using Fookbase.Api.Modules.Ai.Controllers;
using Fookbase.Api.Modules.Ai.DTOs.Requests;
using Fookbase.Api.Modules.Ai.DTOs.Responses;
using Fookbase.Api.Modules.Ai.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Ai.Api.IntegrationTests;

public sealed class AiChatServiceTests
{
    [Theory]
    [InlineData(null, "Nội dung tin nhắn là bắt buộc.")]
    [InlineData("", "Nội dung tin nhắn là bắt buộc.")]
    [InlineData("   ", "Nội dung tin nhắn là bắt buộc.")]
    [InlineData("123456", "Nội dung tin nhắn không được vượt quá 5 ký tự.")]
    public async Task Invalid_message_is_rejected_before_calling_a_provider(string? message, string expectedError)
    {
        var provider = new RecordingHandler(HttpStatusCode.OK,
            """{"choices":[{"message":{"content":"Reply"}}]}""");
        await using var app = CreateApp(provider);
        await app.StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync("/api/ai/chat", new AiChatRequest(message, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = Assert.Single(body.RootElement.GetProperty("errors").EnumerateObject(),
            error => error.Name.EndsWith("Message", StringComparison.Ordinal));
        Assert.Contains(error.Value.EnumerateArray(), item => item.GetString() == expectedError);
        Assert.Empty(provider.Requests);
    }

    [Fact]
    public async Task Annotations_preserve_trimmed_lengths_history_filtering_and_history_limit()
    {
        var provider = new RecordingHandler(HttpStatusCode.OK,
            """{"choices":[{"message":{"content":"Reply"}}]}""");
        await using var app = CreateApp(provider, maximumHistoryMessages: 7);
        await app.StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync("/api/ai/chat", new AiChatRequest(" 12345 ", [
            new AiChatHistoryMessage("user", "old"),
            new AiChatHistoryMessage("assistant", " 12345 "),
            new AiChatHistoryMessage("system", "text"),
            new AiChatHistoryMessage(null, "text"),
            new AiChatHistoryMessage("user", null),
            new AiChatHistoryMessage("user", "   "),
            new AiChatHistoryMessage("user", "123456"),
            new AiChatHistoryMessage("user", "new")
        ]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reply = await response.Content.ReadFromJsonAsync<AiChatResponse>();
        Assert.Equal(new AiChatResponse("Reply", "test-model"), reply);
        using var body = JsonDocument.Parse(Assert.Single(provider.Requests).Body);
        var messages = body.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(4, messages.Length);
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("assistant", messages[1].GetProperty("role").GetString());
        Assert.Equal("12345", messages[1].GetProperty("content").GetString());
        Assert.Equal("new", messages[2].GetProperty("content").GetString());
        Assert.Equal("user", messages[3].GetProperty("role").GetString());
        Assert.Equal("12345", messages[3].GetProperty("content").GetString());
    }

    [Fact]
    public async Task Chat_route_uses_a_controller_and_requires_authentication_and_rate_limiting()
    {
        var provider = new RecordingHandler(HttpStatusCode.OK, "{}");
        await using var app = CreateApp(provider, authenticated: false);
        await app.StartAsync();
        var endpoint = Assert.Single(app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>(), item => item.RoutePattern.RawText?.Trim('/') == "api/ai/chat");

        Assert.Equal(typeof(AiChatController), endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()!.ControllerTypeInfo);
        Assert.NotNull(endpoint.Metadata.GetMetadata<IAuthorizeData>());
        Assert.Equal("ai-chat", endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()!.PolicyName);
        Assert.Equal(["POST"], endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
        using var client = app.GetTestClient();
        using var response = await client.PostAsJsonAsync("/api/ai/chat", new AiChatRequest("12345", null));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(provider.Requests);
    }

    [Fact]
    public async Task Chat_controller_rejects_requests_after_the_rate_limit_is_reached()
    {
        var provider = new RecordingHandler(HttpStatusCode.OK,
            """{"choices":[{"message":{"content":"Reply"}}]}""");
        await using var app = CreateApp(provider, permitLimit: 1);
        await app.StartAsync();
        using var client = app.GetTestClient();

        using var first = await client.PostAsJsonAsync("/api/ai/chat", new AiChatRequest("12345", null));
        using var second = await client.PostAsJsonAsync("/api/ai/chat", new AiChatRequest("12345", null));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Single(provider.Requests);
    }

    [Fact]
    public async Task Uses_groq_first_with_openai_compatible_request()
    {
        var groq = new RecordingHandler(HttpStatusCode.OK,
            """{"choices":[{"message":{"content":"Trả lời từ Groq"}}]}""");
        using var services = CreateServices(new AiChatOptions
        {
            Enabled = true,
            Groq = Provider("groq-key", "groq-model", "https://api.groq.com/openai/v1/")
        }, ("ai-chat-groq", groq));
        var service = services.GetRequiredService<AiChatService>();

        var result = await service.ChatAsync(new AiChatRequest("Tin nhắn mới", [
            new AiChatHistoryMessage("assistant", "Tin nhắn trước đó")
        ]), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Trả lời từ Groq", result.Response!.Content);
        Assert.Equal("groq-model", result.Response.Model);
        var request = Assert.Single(groq.Requests);
        Assert.Equal("Bearer groq-key", request.Authorization);
        Assert.Equal("/openai/v1/chat/completions", request.Path);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("groq-model", body.RootElement.GetProperty("model").GetString());
        var messages = body.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("assistant", messages[1].GetProperty("role").GetString());
        Assert.Equal("user", messages[2].GetProperty("role").GetString());
    }

    [Fact]
    public async Task Falls_back_from_groq_to_gemini_when_quota_is_exhausted()
    {
        var groq = new RecordingHandler(HttpStatusCode.TooManyRequests, "{}");
        var gemini = new RecordingHandler(HttpStatusCode.OK,
            """{"candidates":[{"content":{"parts":[{"text":"Trả lời từ Gemini"}]}}]}""");
        using var services = CreateServices(new AiChatOptions
        {
            Enabled = true,
            Groq = Provider("groq-key", "groq-model", "https://api.groq.com/openai/v1/"),
            Gemini = Provider("gemini-key", "gemini-model", "https://generativelanguage.googleapis.com/")
        },
            ("ai-chat-groq", groq),
            ("ai-chat-gemini", gemini));
        var service = services.GetRequiredService<AiChatService>();

        var result = await service.ChatAsync(new AiChatRequest("Xin chào", [
            new AiChatHistoryMessage("assistant", "Mình có thể giúp gì?")
        ]), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Trả lời từ Gemini", result.Response!.Content);
        Assert.Equal("gemini-model", result.Response.Model);
        var geminiRequest = Assert.Single(gemini.Requests);
        Assert.Equal("gemini-key", geminiRequest.GeminiApiKey);
        Assert.Equal("/v1beta/models/gemini-model:generateContent", geminiRequest.Path);
        using var geminiBody = JsonDocument.Parse(geminiRequest.Body);
        var contents = geminiBody.RootElement.GetProperty("contents").EnumerateArray().ToArray();
        Assert.Equal("model", contents[0].GetProperty("role").GetString());
        Assert.Equal("user", contents[1].GetProperty("role").GetString());
    }

    [Fact]
    public async Task Falls_back_to_openrouter_after_groq_and_gemini_are_unavailable()
    {
        var groq = new RecordingHandler(HttpStatusCode.ServiceUnavailable, "{}");
        var gemini = new RecordingHandler(HttpStatusCode.TooManyRequests, "{}");
        var openRouter = new RecordingHandler(HttpStatusCode.OK,
            """{"choices":[{"message":{"content":"Trả lời từ OpenRouter"}}]}""");
        using var services = CreateServices(new AiChatOptions
        {
            Enabled = true,
            Groq = Provider("groq-key", "groq-model", "https://api.groq.com/openai/v1/"),
            Gemini = Provider("gemini-key", "gemini-model", "https://generativelanguage.googleapis.com/"),
            OpenRouter = Provider("openrouter-key", "openrouter/free", "https://openrouter.ai/api/v1/")
        },
            ("ai-chat-groq", groq),
            ("ai-chat-gemini", gemini),
            ("ai-chat-openrouter", openRouter));
        var service = services.GetRequiredService<AiChatService>();

        var result = await service.ChatAsync(new AiChatRequest("Xin chào", null), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Trả lời từ OpenRouter", result.Response!.Content);
        Assert.Equal("openrouter/free", result.Response.Model);
        var request = Assert.Single(openRouter.Requests);
        Assert.Equal("Bearer openrouter-key", request.Authorization);
        Assert.Equal("/api/v1/chat/completions", request.Path);
    }

    [Fact]
    public async Task Does_not_fall_back_for_invalid_provider_credentials()
    {
        var groq = new RecordingHandler(HttpStatusCode.Unauthorized, "{}");
        var gemini = new RecordingHandler(HttpStatusCode.OK,
            """{"candidates":[{"content":{"parts":[{"text":"Không được gọi"}]}}]}""");
        using var services = CreateServices(new AiChatOptions
        {
            Enabled = true,
            Groq = Provider("bad-groq-key", "groq-model"),
            Gemini = Provider("gemini-key", "gemini-model")
        }, ("ai-chat-groq", groq), ("ai-chat-gemini", gemini));
        var service = services.GetRequiredService<AiChatService>();

        var result = await service.ChatAsync(new AiChatRequest("Xin chào", null), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        Assert.Equal("Dịch vụ AI không thể hoàn thành yêu cầu.", result.Error);
        Assert.Empty(gemini.Requests);
    }

    [Fact]
    public async Task Keeps_the_legacy_openai_configuration_working()
    {
        var openAi = new RecordingHandler(HttpStatusCode.OK, """{"output_text":"Trả lời từ OpenAI"}""");
        using var services = CreateServices(new AiChatOptions
        {
            Enabled = true,
            ApiKey = "legacy-key",
            ApiBaseUrl = "https://api.openai.com/",
            Model = "legacy-model"
        }, ("ai-chat-openai", openAi));
        var service = services.GetRequiredService<AiChatService>();

        var result = await service.ChatAsync(new AiChatRequest("Xin chào", null), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Trả lời từ OpenAI", result.Response!.Content);
        Assert.Equal("legacy-model", result.Response.Model);
        var request = Assert.Single(openAi.Requests);
        Assert.Equal("Bearer legacy-key", request.Authorization);
        Assert.Equal("/v1/responses", request.Path);
    }

    private static WebApplication CreateApp(
        RecordingHandler provider,
        int maximumHistoryMessages = 10,
        bool authenticated = true,
        int permitLimit = 100)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(AiChatController).Assembly);
        builder.Services.AddProblemDetails();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter("ai-chat", limiter =>
            {
                limiter.PermitLimit = permitLimit;
                limiter.Window = TimeSpan.FromMinutes(1);
            });
        });
        builder.Services.AddAiInfrastructure(new AiChatOptions
        {
            Enabled = true,
            MaximumInputCharacters = 5,
            MaximumHistoryMessages = maximumHistoryMessages,
            Groq = Provider("test-key", "test-model")
        });
        builder.Services.AddHttpClient("ai-chat-groq").ConfigurePrimaryHttpMessageHandler(() => provider);
        var app = builder.Build();
        app.UseAuthentication();
        if (authenticated)
        {
            var userId = Guid.NewGuid().ToString();
            app.Use((context, next) =>
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(JwtRegisteredClaimNames.Sub, userId)], "ai-test"));
                return next(context);
            });
        }
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapControllers();
        return app;
    }

    private static ServiceProvider CreateServices(
        AiChatOptions options,
        params (string Name, RecordingHandler Handler)[] handlers)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAiInfrastructure(options);
        foreach (var handler in handlers)
        {
            services.AddHttpClient(handler.Name)
                .ConfigurePrimaryHttpMessageHandler(() => handler.Handler);
        }

        return services.BuildServiceProvider();
    }

    private static AiChatProviderOptions Provider(string apiKey, string model, string baseUrl = "https://provider.test/") => new()
    {
        ApiKey = apiKey,
        ApiBaseUrl = baseUrl,
        Model = model
    };

    private sealed class RecordingHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.RequestUri?.AbsolutePath,
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("x-goog-api-key", out var apiKeys) ? apiKeys.Single() : null,
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record RecordedRequest(string? Path, string? Authorization, string? GeminiApiKey, string Body);
}
