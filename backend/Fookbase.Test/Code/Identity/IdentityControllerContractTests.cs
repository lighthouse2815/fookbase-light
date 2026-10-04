using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class IdentityControllerContractTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    // Preserve the HTTP contract from the Minimal API implementation during the migration.
    private static readonly (string Method, string Path, bool Authorized, string? RateLimit)[] Routes =
    [
        ("GET", "/api/auth/providers", false, null),
        ("GET", "/api/auth/google/start", false, "auth-login"),
        ("GET", "/api/auth/google/callback", false, null),
        ("POST", "/api/auth/google/exchange", false, "auth-login"),
        ("POST", "/api/auth/google/link", false, "auth-login"),
        ("POST", "/api/auth/registration/start", false, "auth-sensitive"),
        ("POST", "/api/auth/registration/resend", false, "auth-sensitive"),
        ("POST", "/api/auth/registration/verify", false, "auth-sensitive"),
        ("POST", "/api/auth/login", false, "auth-login"),
        ("POST", "/api/auth/2fa/verify", false, "auth-login"),
        ("POST", "/api/auth/refresh", false, null),
        ("POST", "/api/auth/password/forgot", false, "auth-password-recovery"),
        ("POST", "/api/auth/password/reset", false, "auth-password-recovery"),
        ("POST", "/api/auth/email/verify", false, null),
        ("POST", "/api/auth/logout", true, null),
        ("POST", "/api/auth/password/change", true, null),
        ("GET", "/api/auth/sessions", true, null),
        ("DELETE", "/api/auth/sessions/{sessionId:guid}", true, null),
        ("POST", "/api/auth/sessions/revoke-others", true, null),
        ("GET", "/api/auth/security", true, null),
        ("POST", "/api/auth/2fa/setup", true, "auth-sensitive"),
        ("POST", "/api/auth/2fa/enable", true, "auth-sensitive"),
        ("POST", "/api/auth/2fa/disable", true, "auth-sensitive"),
        ("POST", "/api/auth/2fa/recovery-codes/regenerate", true, "auth-sensitive"),
        ("POST", "/api/auth/email/verification", true, "auth-resend-verification"),
        ("GET", "/api/auth/me", true, null),
        ("GET", "/api/auth/google/mobile/start", false, "auth-login"),
        ("GET", "/api/auth/google/mobile/callback", false, "auth-login"),
        ("POST", "/api/auth/google/mobile/exchange", false, "auth-login"),
        ("POST", "/api/auth/google/mobile/link", false, "auth-login"),
    ];

    [Fact]
    public void Routes_keep_their_methods_authorization_and_rate_limits()
    {
        using var client = factory.CreateClient();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.TrimStart('/').StartsWith("api/auth/") == true)
            .ToArray();
        Assert.Equal(Routes.Length, endpoints.Length);

        foreach (var route in Routes)
        {
            var endpoint = Assert.Single(endpoints, item =>
                "/" + item.RoutePattern.RawText!.TrimStart('/') == route.Path &&
                item.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(route.Method));
            Assert.NotNull(endpoint.Metadata.GetMetadata<ControllerActionDescriptor>());
            Assert.Equal(route.Authorized, endpoint.Metadata.GetMetadata<IAuthorizeData>() is not null);
            Assert.Equal(!route.Authorized, endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null);
            Assert.Equal(route.RateLimit, endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName);
        }
    }

    [Fact]
    public async Task Protected_routes_reject_anonymous_requests()
    {
        using var client = factory.CreateClient();
        foreach (var route in Routes.Where(route => route.Authorized))
        {
            var path = route.Path.Replace("{sessionId:guid}", Guid.NewGuid().ToString());
            using var request = new HttpRequestMessage(new HttpMethod(route.Method), path);
            if (route.Method == "POST") request.Content = JsonContent.Create(new { });
            using var response = await client.SendAsync(request);
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized,
                $"{route.Method} {route.Path} returned {response.StatusCode}.");
            await AssertErrorAsync(response, "invalid_access_token");
        }
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("")]
    public async Task Invalid_body_returns_an_api_error_response(string body)
    {
        using var client = factory.CreateClient();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/auth/login", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_request", document.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Login_rate_limit_is_enforced_for_controller_actions()
    {
        using var limitedFactory = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:SensitiveAuth:LoginPermitLimit", "1"));
        using var client = limitedFactory.CreateClient();
        using var first = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(null, null));
        using var second = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(null, null));
        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        await AssertErrorAsync(second, "rate_limit_exceeded");
    }

    [Fact]
    public async Task Successful_responses_share_the_envelope_with_and_without_data()
    {
        using var client = factory.CreateClient();
        using var providers = await client.GetAsync("/api/auth/providers");
        using var document = JsonDocument.Parse(await providers.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, providers.StatusCode);
        var body = document.RootElement;
        Assert.Equal(4, body.EnumerateObject().Count());
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.True(body.GetProperty("data").GetProperty("google").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("error").ValueKind);
        Assert.Equal(providers.Headers.GetValues("X-Request-Id").Single(), body.GetProperty("requestId").GetString());

        using var forgot = await client.PostAsJsonAsync("/api/auth/password/forgot",
            new { identifier = $"missing-{Guid.NewGuid():N}@example.test" });
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        using var empty = JsonDocument.Parse(await forgot.Content.ReadAsStringAsync());
        Assert.True(empty.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Null, empty.RootElement.GetProperty("data").ValueKind);
        Assert.Equal(JsonValueKind.Null, empty.RootElement.GetProperty("error").ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(empty.RootElement.GetProperty("requestId").GetString()));
    }

    [Theory]
    [InlineData("GET", "/api/auth/missing", "application/json", 404, "not_found")]
    [InlineData("GET", "/api/auth/login", "application/json", 405, "method_not_allowed")]
    [InlineData("POST", "/api/auth/login", "text/plain", 415, "unsupported_media_type")]
    [InlineData("GET", "/api/auth/google/mobile/callback", "application/json", 404, "not_found")]
    public async Task Http_boundary_errors_use_the_same_envelope(
        string method, string path, string contentType, int statusCode, string code)
    {
        using var app = factory.WithWebHostBuilder(builder => builder.UseSetting("GoogleAuthentication:MobileCallbackUrl", ""));
        using var client = app.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST") request.Content = new StringContent("{}", Encoding.UTF8, contentType);
        using var response = await client.SendAsync(request);

        Assert.Equal(statusCode, (int)response.StatusCode);
        await AssertErrorAsync(response, code);
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, string code)
    {
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement;
        Assert.Equal(4, body.EnumerateObject().Count());
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("data").ValueKind);
        Assert.Equal(code, body.GetProperty("error").GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("error").GetProperty("message").GetString()));
        Assert.Equal(response.Headers.GetValues("X-Request-Id").Single(), body.GetProperty("requestId").GetString());
    }

    [Theory]
    [InlineData("Testing")]
    [InlineData("Development")]
    public async Task Legacy_registration_is_not_exposed_in_any_environment(string environment)
    {
        using var environmentFactory = factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment));
        using var client = environmentFactory.CreateClient();
        var request = new
        {
            email = $"removed-registration-{Guid.NewGuid():N}@example.test",
            username = $"removed.{Guid.NewGuid():N}"[..32],
            password = "Password123!",
        };
        using var response = await client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var endpoints = environmentFactory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>();
        Assert.DoesNotContain(endpoints, endpoint => endpoint.RoutePattern.RawText?.TrimStart('/') == "api/auth/register");

        var authentication = await TestAccountSetup.CreateAsync(environmentFactory, client,
            request.email, request.username, request.password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authentication.AccessToken);
        using var authenticatedResponse = await client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.NotFound, authenticatedResponse.StatusCode);
    }
}
