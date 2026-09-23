using System.Net;
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
        ("POST", "/api/auth/register", false, null),
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
        }
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("")]
    public async Task Invalid_body_returns_a_problem_response(string body)
    {
        using var client = factory.CreateClient();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/auth/login", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_request", document.RootElement.GetProperty("code").GetString());
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
    }

    [Fact]
    public async Task Legacy_registration_is_not_exposed_outside_testing()
    {
        using var developmentFactory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = developmentFactory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(null, null, null));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var endpoints = developmentFactory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>();
        Assert.DoesNotContain(endpoints, endpoint => endpoint.RoutePattern.RawText?.TrimStart('/') == "api/auth/register");
    }
}
