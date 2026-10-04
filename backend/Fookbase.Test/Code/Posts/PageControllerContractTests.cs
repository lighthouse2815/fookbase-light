using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class PageControllerContractTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    [Fact]
    public async Task Page_routes_keep_methods_authorization_and_controller_registration()
    {
        using var client = factory.CreateClient();
        var routes = new (string Method, string Path, bool Authorized)[]
        {
            ("GET", "api/pages/discover", false),
            ("GET", "api/pages/mine", true),
            ("GET", "api/pages/following", true),
            ("GET", "api/pages/invitations/mine", true),
            ("POST", "api/pages/invitations/{inviteId:guid}/accept", true),
            ("POST", "api/pages/invitations/{inviteId:guid}/decline", true),
            ("POST", "api/pages", true),
            ("GET", "api/pages/{idOrUsername}", false),
            ("PATCH", "api/pages/{pageId:guid}", true),
            ("PATCH", "api/pages/{pageId:guid}/media", true),
            ("DELETE", "api/pages/{pageId:guid}", true),
            ("POST", "api/pages/{pageId:guid}/publish", true),
            ("POST", "api/pages/{pageId:guid}/unpublish", true),
            ("GET", "api/pages/{pageId:guid}/avatar", false),
            ("GET", "api/pages/{pageId:guid}/cover", false),
            ("POST", "api/pages/{pageId:guid}/follow", true),
            ("DELETE", "api/pages/{pageId:guid}/follow", true),
            ("GET", "api/pages/{pageId:guid}/members", true),
            ("POST", "api/pages/{pageId:guid}/invitations", true),
            ("PATCH", "api/pages/{pageId:guid}/members/{userId:guid}/role", true),
            ("DELETE", "api/pages/{pageId:guid}/members/{userId:guid}", true),
            ("POST", "api/pages/{pageId:guid}/transfer-ownership", true),
            ("GET", "api/pages/{pageId:guid}/posts", false),
            ("POST", "api/pages/{pageId:guid}/posts", true),
        };
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.TrimStart('/').StartsWith("api/pages") == true)
            .ToArray();
        Assert.Equal(routes.Length, endpoints.Length);
        foreach (var route in routes)
        {
            var endpoint = Assert.Single(endpoints, item =>
                item.RoutePattern.RawText!.TrimStart('/') == route.Path &&
                item.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(route.Method));
            Assert.NotNull(endpoint.Metadata.GetMetadata<ControllerActionDescriptor>());
            Assert.Equal(route.Authorized, endpoint.Metadata.GetMetadata<IAuthorizeData>() is not null);
            Assert.Equal(!route.Authorized, endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null);
            if (!route.Authorized) continue;
            var path = "/" + route.Path.Replace("{pageId:guid}", Guid.NewGuid().ToString())
                .Replace("{inviteId:guid}", Guid.NewGuid().ToString()).Replace("{userId:guid}", Guid.NewGuid().ToString());
            using var request = new HttpRequestMessage(new HttpMethod(route.Method), path);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Public_page_routes_keep_query_binding_and_problem_bodies()
    {
        using var client = factory.CreateClient();
        using var discover = await client.GetAsync("/api/pages/discover?limit=1");
        Assert.Equal(HttpStatusCode.OK, discover.StatusCode);
        using var invalidLimit = await client.GetAsync("/api/pages/discover?limit=0");
        Assert.Equal(HttpStatusCode.BadRequest, invalidLimit.StatusCode);
        using var problem = JsonDocument.Parse(await invalidLimit.Content.ReadAsStringAsync());
        Assert.Equal("invalid_pagination", problem.RootElement.GetProperty("code").GetString());
        using var malformedLimit = await client.GetAsync("/api/pages/discover?limit=invalid");
        Assert.Equal(HttpStatusCode.BadRequest, malformedLimit.StatusCode);
        foreach (var suffix in new[] { "", "/posts", "/avatar", "/cover" })
        {
            using var missing = await client.GetAsync($"/api/pages/{Guid.NewGuid()}{suffix}");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
    }
}
