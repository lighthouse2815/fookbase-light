using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fookbase.Api.Modules.Posts.DTOs.Responses;
using Fookbase.Api.Shared.ErrorHandling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class PostsControllerContractTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    private static readonly (string Method, string Path, bool Authorized)[] Routes =
    [
        ("POST", "/api/posts", true),
        ("GET", "/api/posts/saved", true),
        ("PUT", "/api/posts/{postId:guid}", true),
        ("DELETE", "/api/posts/{postId:guid}", true),
        ("PUT", "/api/posts/{postId:guid}/pin", true),
        ("DELETE", "/api/posts/{postId:guid}/pin", true),
        ("GET", "/api/posts/{postId:guid}", false),
        ("GET", "/api/posts/feed", true),
        ("GET", "/api/posts/search", true),
        ("GET", "/api/posts/users/{authorUserId:guid}", false),
        ("POST", "/api/posts/{postId:guid}/comments", true),
        ("GET", "/api/posts/{postId:guid}/comments", false),
        ("GET", "/api/posts/{postId:guid}/reactions", true),
        ("PUT", "/api/posts/comments/{commentId:guid}", true),
        ("DELETE", "/api/posts/comments/{commentId:guid}", true),
        ("PUT", "/api/posts/comments/{commentId:guid}/reaction", true),
        ("DELETE", "/api/posts/comments/{commentId:guid}/reaction", true),
        ("PUT", "/api/posts/{postId:guid}/reaction", true),
        ("DELETE", "/api/posts/{postId:guid}/reaction", true),
        ("POST", "/api/posts/{postId:guid}/save", true),
        ("DELETE", "/api/posts/{postId:guid}/save", true),
        ("POST", "/api/posts/{postId:guid}/shares", true),
        ("GET", "/api/posts/{postId:guid}/media/{mediaId:guid}/access", true),
        ("GET", "/api/hashtags/{tag}/posts", false),
        ("POST", "/api/reports/users/{userId:guid}", true),
        ("POST", "/api/reports/posts/{postId:guid}", true),
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
            using var request = new HttpRequestMessage(new HttpMethod(route.Method), ToRequestPath(route.Path));
            if (route.Method is "POST" or "PUT") request.Content = JsonContent.Create(new { });

            using var response = await client.SendAsync(request);

            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized,
                $"{route.Method} {route.Path} returned {response.StatusCode}.");
        }
    }

    [Fact]
    public async Task Public_routes_allow_anonymous_requests_and_keep_pagination_defaults()
    {
        using var client = factory.CreateClient();
        using var posts = await client.GetAsync($"/api/posts/users/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.OK, posts.StatusCode);
        var page = await posts.Content.ReadFromJsonAsync<PagedResponse<PostResponse>>();
        Assert.NotNull(page);
        Assert.Equal(0, page.Offset);
        Assert.Equal(20, page.Limit);

        using var hashtags = await client.GetAsync("/api/hashtags/controllercontract/posts");
        Assert.Equal(HttpStatusCode.OK, hashtags.StatusCode);
        Assert.NotNull(await hashtags.Content.ReadFromJsonAsync<HashtagPostsPageResponse>());

        foreach (var suffix in new[] { "", "/comments" })
        {
            using var missing = await client.GetAsync($"/api/posts/{Guid.NewGuid()}{suffix}");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid-user-id")]
    public async Task Authenticated_requests_without_a_valid_subject_are_rejected(string? subject)
    {
        using var client = CreateAuthenticatedClient(subject);
        foreach (var route in Routes.Where(route => !route.Authorized || route.Path == "/api/posts/saved"))
        {
            var path = ToRequestPath(route.Path).Replace("{tag}", "controllercontract");
            using var response = await client.GetAsync(path);

            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized,
                $"{route.Path} returned {response.StatusCode}.");
        }
    }

    [Theory]
    [InlineData("POST", "/api/posts")]
    [InlineData("PUT", "/api/posts/{postId:guid}")]
    [InlineData("POST", "/api/posts/{postId:guid}/comments")]
    [InlineData("PUT", "/api/posts/comments/{commentId:guid}")]
    [InlineData("PUT", "/api/posts/{postId:guid}/reaction")]
    [InlineData("PUT", "/api/posts/comments/{commentId:guid}/reaction")]
    [InlineData("POST", "/api/posts/{postId:guid}/shares")]
    [InlineData("POST", "/api/reports/posts/{postId:guid}")]
    [InlineData("POST", "/api/reports/users/{userId:guid}")]
    public async Task Malformed_json_returns_a_bad_request(string method, string path)
    {
        using var client = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        using var request = new HttpRequestMessage(new HttpMethod(method), ToRequestPath(path))
        {
            Content = new StringContent("{", Encoding.UTF8, "application/json")
        };
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ErrorCode.InvalidRequest.Code, body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Invalid_pagination_returns_a_bad_request()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/posts/users/{Guid.NewGuid()}?offset=invalid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient(string? subject)
    {
        using var scope = factory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var now = DateTime.UtcNow;
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) };
        if (subject is not null) claims.Add(new Claim(JwtRegisteredClaimNames.Sub, subject));
        var token = new JwtSecurityToken(
            configuration["Jwt:Issuer"],
            configuration["Jwt:Audience"],
            claims,
            notBefore: now.AddSeconds(-1),
            expires: now.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!)),
                SecurityAlgorithms.HmacSha256));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private static string ToRequestPath(string path) =>
        Regex.Replace(path, @"\{\w+:guid\}", _ => Guid.NewGuid().ToString());
}
