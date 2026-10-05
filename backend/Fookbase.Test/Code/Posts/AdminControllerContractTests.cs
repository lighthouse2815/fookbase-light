using System.Net;
using Fookbase.Api.Modules.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class AdminControllerContractTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    [Fact]
    public async Task Admin_routes_keep_methods_authorization_and_controller_registration()
    {
        using var client = factory.CreateClient();
        var routes = new (string Method, string Path)[]
        {
            ("GET", "api/admin/dashboard"),
            ("GET", "api/admin/users"),
            ("PATCH", "api/admin/users/{userId:guid}/status"),
            ("GET", "api/admin/reports"),
            ("PATCH", "api/admin/reports/{reportId:guid}/status"),
            ("GET", "api/admin/reports/{reportId:guid}"),
            ("POST", "api/admin/reports/{reportId:guid}/dismiss"),
            ("POST", "api/admin/reports/{reportId:guid}/remove-content"),
            ("POST", "api/admin/reports/{reportId:guid}/warn-user"),
            ("POST", "api/admin/reports/{reportId:guid}/suspend-user"),
            ("GET", "api/admin/users/{userId:guid}/moderation-state"),
            ("GET", "api/admin/users/{userId:guid}/moderation-history"),
            ("POST", "api/admin/users/{userId:guid}/warn"),
            ("POST", "api/admin/users/{userId:guid}/suspend"),
            ("POST", "api/admin/users/{userId:guid}/unsuspend"),
            ("POST", "api/admin/users/{userId:guid}/disable"),
            ("POST", "api/admin/users/{userId:guid}/enable"),
            ("DELETE", "api/admin/posts/{postId:guid}")
        };
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.Trim('/').StartsWith("api/admin/") == true)
            .ToArray();

        Assert.Equal(routes.Length, endpoints.Length);
        foreach (var route in routes)
        {
            var endpoint = Assert.Single(endpoints, item =>
                item.RoutePattern.RawText!.Trim('/') == route.Path &&
                item.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(route.Method));
            Assert.NotNull(endpoint.Metadata.GetMetadata<ControllerActionDescriptor>());
            Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(), item => item.Policy == AdminPolicy.Name);
            var path = "/" + route.Path
                .Replace("{userId:guid}", Guid.NewGuid().ToString())
                .Replace("{reportId:guid}", Guid.NewGuid().ToString())
                .Replace("{postId:guid}", Guid.NewGuid().ToString());
            using var request = new HttpRequestMessage(new HttpMethod(route.Method), path);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
