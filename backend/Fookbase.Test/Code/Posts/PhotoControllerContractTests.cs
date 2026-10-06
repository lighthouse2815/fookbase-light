using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class PhotoControllerContractTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    [Fact]
    public async Task Photo_routes_keep_methods_authorization_and_controller_registration()
    {
        using var client = factory.CreateClient();
        var routes = new (string Method, string Path, bool Authorized)[]
        {
            ("POST", "api/albums", true),
            ("GET", "api/albums/{albumId:guid}", false),
            ("PATCH", "api/albums/{albumId:guid}", true),
            ("DELETE", "api/albums/{albumId:guid}", true),
            ("POST", "api/albums/{albumId:guid}/media", true),
            ("GET", "api/albums/{albumId:guid}/media", false),
            ("GET", "api/albums/{albumId:guid}/media/{mediaId:guid}", false),
            ("GET", "api/albums/{albumId:guid}/media/{mediaId:guid}/access", false),
            ("PATCH", "api/albums/{albumId:guid}/media/{mediaId:guid}", true),
            ("DELETE", "api/albums/{albumId:guid}/media/{mediaId:guid}", true),
            ("GET", "api/users/{userId:guid}/albums", false)
        };
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.Trim('/').StartsWith("api/albums") == true ||
                endpoint.RoutePattern.RawText?.Trim('/') == "api/users/{userId:guid}/albums")
            .ToArray();
        Assert.Equal(routes.Length, endpoints.Length);
        foreach (var route in routes)
        {
            var endpoint = Assert.Single(endpoints, item =>
                item.RoutePattern.RawText!.Trim('/') == route.Path &&
                item.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(route.Method));
            Assert.NotNull(endpoint.Metadata.GetMetadata<ControllerActionDescriptor>());
            Assert.Equal(route.Authorized, endpoint.Metadata.GetMetadata<IAuthorizeData>() is not null);
            Assert.Equal(!route.Authorized, endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null);
            if (!route.Authorized) continue;
            var path = "/" + route.Path.Replace("{albumId:guid}", Guid.NewGuid().ToString())
                .Replace("{mediaId:guid}", Guid.NewGuid().ToString());
            using var request = new HttpRequestMessage(new HttpMethod(route.Method), path);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("name", null, "Name")]
    [InlineData("name", "   ", "Name")]
    [InlineData("privacy", null, "Privacy")]
    [InlineData("privacy", "   ", "Privacy")]
    [InlineData("privacy", "unknown", "Privacy")]
    [InlineData("privacy", "99", "Privacy")]
    public async Task Invalid_album_fields_are_rejected_with_field_errors(string field, string? value, string member)
    {
        using var client = CreateAuthenticatedClient();
        var body = new Dictionary<string, object?> { ["name"] = "Photo album", ["privacy"] = "public" };
        body[field] = value;
        using var create = await client.PostAsJsonAsync("/api/albums", body);
        await AssertValidationAsync(create, member);
        using var update = await client.PatchAsJsonAsync($"/api/albums/{Guid.NewGuid()}", body);
        await AssertValidationAsync(update, member);
    }

    [Theory]
    [InlineData("name", 161, "Name")]
    [InlineData("description", 2_001, "Description")]
    public async Task Overlong_album_text_is_rejected_with_field_errors(string field, int length, string member)
    {
        using var client = CreateAuthenticatedClient();
        var body = new Dictionary<string, object?> { ["name"] = "Photo album", ["privacy"] = "public" };
        body[field] = new string('a', length);
        using var create = await client.PostAsJsonAsync("/api/albums", body);
        await AssertValidationAsync(create, member);
        using var update = await client.PatchAsJsonAsync($"/api/albums/{Guid.NewGuid()}", body);
        await AssertValidationAsync(update, member);
    }

    [Fact]
    public async Task Empty_media_id_and_overlong_caption_are_rejected_before_album_lookup()
    {
        using var client = CreateAuthenticatedClient();
        var path = $"/api/albums/{Guid.NewGuid()}/media";
        using var add = await client.PostAsJsonAsync(path, new { mediaId = Guid.Empty });
        await AssertValidationAsync(add, "MediaId");
        using var update = await client.PatchAsJsonAsync($"{path}/{Guid.NewGuid()}", new { caption = new string('a', 1_001) });
        await AssertValidationAsync(update, "Caption");
    }

    [Theory]
    [InlineData("public")]
    [InlineData("friends")]
    [InlineData("only_me")]
    [InlineData("OnlyMe")]
    public async Task Album_validation_preserves_trimmed_limits_and_privacy_names(string privacy)
    {
        using var client = CreateAuthenticatedClient();
        using var update = await client.PatchAsJsonAsync($"/api/albums/{Guid.NewGuid()}", new
        {
            name = " " + new string('a', 160) + " ",
            description = " " + new string('b', 2_000) + " ",
            privacy
        });
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        using var caption = await client.PatchAsJsonAsync($"/api/albums/{Guid.NewGuid()}/media/{Guid.NewGuid()}",
            new { caption = " " + new string('a', 1_000) + " " });
        Assert.Equal(HttpStatusCode.NotFound, caption.StatusCode);
    }

    [Fact]
    public async Task Photo_queries_preserve_limit_validation_and_missing_album_errors()
    {
        using var client = factory.CreateClient();
        var path = $"/api/albums/{Guid.NewGuid()}/media";
        using var invalidLimit = await client.GetAsync($"{path}?limit=0");
        Assert.Equal(HttpStatusCode.BadRequest, invalidLimit.StatusCode);
        using var problem = JsonDocument.Parse(await invalidLimit.Content.ReadAsStringAsync());
        Assert.Equal("invalid_limit", problem.RootElement.GetProperty("code").GetString());
        using var malformedLimit = await client.GetAsync($"{path}?limit=invalid");
        Assert.Equal(HttpStatusCode.BadRequest, malformedLimit.StatusCode);
        using var missing = await client.GetAsync($"{path}?limit=1");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient()
    {
        using var scope = factory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"],
            [new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())], now.AddSeconds(-1), now.AddMinutes(5),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!)),
                SecurityAlgorithms.HmacSha256));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private static async Task AssertValidationAsync(HttpResponseMessage response, string member)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("validation_failed", document.RootElement.GetProperty("code").GetString());
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty(member, out _),
            await response.Content.ReadAsStringAsync());
    }
}
