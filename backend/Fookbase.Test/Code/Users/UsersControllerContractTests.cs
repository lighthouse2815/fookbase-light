using System.Net;
using System.Net.Http.Json;
using Fookbase.Api.Modules.Users.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Users.Api.IntegrationTests;

public sealed class UsersControllerContractTests(UsersApiFactory factory) : IClassFixture<UsersApiFactory>
{
    private static readonly (string Method, string Path, bool Authorized)[] Routes =
    [
        ("GET", "/api/users/search", false),
        ("GET", "/api/users/{userId:guid}/avatar", false),
        ("GET", "/api/users/{userId:guid}/cover", false),
        ("GET", "/api/users/{userId:guid}", false),
        ("GET", "/api/users/me", true),
        ("PATCH", "/api/users/me", true),
        ("POST", "/api/users/{userId:guid}/follow", true),
        ("DELETE", "/api/users/{userId:guid}/follow", true),
        ("GET", "/api/users/{userId:guid}/followers", true),
        ("GET", "/api/users/{userId:guid}/following", true),
        ("GET", "/api/users/{userId:guid}/friends", true),
        ("GET", "/api/birthdays/today", true),
        ("GET", "/api/birthdays/upcoming", true),
        ("GET", "/api/privacy", true),
        ("PATCH", "/api/privacy", true),
    ];

    [Fact]
    public void Controllers_preserve_routes_methods_authorization_and_rate_limits()
    {
        using var client = factory.CreateClient();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.Trim('/') is { } path &&
                Routes.Any(route => route.Path == "/" + path))
            .ToArray();
        Assert.True(Routes.Length == endpoints.Length,
            string.Join(Environment.NewLine, endpoints.Select(endpoint => endpoint.RoutePattern.RawText)));

        foreach (var route in Routes)
        {
            var endpoint = Assert.Single(endpoints, item =>
                "/" + item.RoutePattern.RawText!.Trim('/') == route.Path &&
                item.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(route.Method));
            Assert.NotNull(endpoint.Metadata.GetMetadata<ControllerActionDescriptor>());
            Assert.Equal(route.Authorized, endpoint.Metadata.GetMetadata<IAuthorizeData>() is not null);
            Assert.Equal(!route.Authorized, endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null);
            Assert.Null(endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>());
        }
    }

    [Fact]
    public async Task Protected_routes_reject_anonymous_requests()
    {
        using var client = factory.CreateClient();
        foreach (var route in Routes.Where(route => route.Authorized))
        {
            var path = route.Path.Replace("{userId:guid}", Guid.NewGuid().ToString());
            using var request = new HttpRequestMessage(new HttpMethod(route.Method), path);
            if (route.Method is "POST" or "PATCH") request.Content = JsonContent.Create(new { });

            using var response = await client.SendAsync(request);

            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized,
                $"{route.Method} {route.Path} returned {response.StatusCode}.");
        }
    }

    [Fact]
    public async Task Public_routes_allow_anonymous_requests_and_keep_search_pagination_defaults()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/users/search");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<UserProfileResponse>>();
        Assert.NotNull(page);
        Assert.Equal(0, page.Offset);
        Assert.Equal(20, page.Limit);

        foreach (var suffix in new[] { "", "/avatar", "/cover" })
        {
            using var missing = await client.GetAsync($"/api/users/{Guid.NewGuid()}{suffix}");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
    }
}
