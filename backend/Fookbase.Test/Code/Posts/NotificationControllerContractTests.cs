using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class NotificationControllerContractTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    [Fact]
    public async Task Notification_routes_keep_methods_authorization_and_controller_registration()
    {
        using var client = factory.CreateClient();
        var routes = new (string Method, string Path)[]
        {
            ("GET", "api/notifications"),
            ("GET", "api/notifications/unread-count"),
            ("POST", "api/notifications/{notificationId:guid}/read"),
            ("POST", "api/notifications/read-all"),
            ("POST", "api/notifications/push-tokens/zola"),
            ("DELETE", "api/notifications/push-tokens/zola"),
        };
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.Trim('/').StartsWith("api/notifications") == true)
            .ToArray();

        Assert.Equal(routes.Length, endpoints.Length);
        foreach (var route in routes)
        {
            var endpoint = Assert.Single(endpoints, item =>
                item.RoutePattern.RawText!.Trim('/') == route.Path &&
                item.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(route.Method));
            Assert.NotNull(endpoint.Metadata.GetMetadata<ControllerActionDescriptor>());
            Assert.NotNull(endpoint.Metadata.GetMetadata<IAuthorizeData>());
            var path = "/" + route.Path.Replace("{notificationId:guid}", Guid.NewGuid().ToString());
            using var request = new HttpRequestMessage(new HttpMethod(route.Method), path);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
