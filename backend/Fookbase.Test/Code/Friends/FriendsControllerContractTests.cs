using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Friends.Api.IntegrationTests;

public sealed class FriendsControllerContractTests(FriendsApiFactory factory) : IClassFixture<FriendsApiFactory>
{
    private static readonly (string Method, string Path)[] Routes =
    [
        ("POST", "/api/friends/requests/{userId:guid}"),
        ("DELETE", "/api/friends/requests/{requestId:guid}"),
        ("POST", "/api/friends/requests/{requestId:guid}/accept"),
        ("POST", "/api/friends/requests/{requestId:guid}/decline"),
        ("GET", "/api/friends/requests/incoming"),
        ("GET", "/api/friends/requests/outgoing"),
        ("GET", "/api/friends/notifications/unread"),
        ("POST", "/api/friends/notifications/{notificationId:guid}/read"),
        ("DELETE", "/api/friends/{userId:guid}"),
        ("GET", "/api/friends"),
        ("GET", "/api/friends/status/{userId:guid}"),
        ("GET", "/api/friends/mutual/{userId:guid}"),
        ("GET", "/api/friends/suggestions"),
        ("POST", "/api/friends/blocks/{userId:guid}"),
        ("DELETE", "/api/friends/blocks/{userId:guid}"),
        ("GET", "/api/friends/blocks"),
    ];

    [Fact]
    public void Friends_routes_use_controllers_and_preserve_methods_and_authorization()
    {
        using var client = factory.CreateClient();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.Trim('/') is { } path &&
                Routes.Any(route => route.Path == "/" + path))
            .ToArray();
        Assert.Equal(Routes.Length, endpoints.Length);
        foreach (var route in Routes)
        {
            var endpoint = Assert.Single(endpoints, item =>
                "/" + item.RoutePattern.RawText!.Trim('/') == route.Path &&
                item.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(route.Method));
            Assert.NotNull(endpoint.Metadata.GetMetadata<ControllerActionDescriptor>());
            Assert.NotNull(endpoint.Metadata.GetMetadata<IAuthorizeData>());
            Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
        }
    }

    [Fact]
    public async Task All_friends_routes_reject_anonymous_requests()
    {
        using var client = factory.CreateClient();
        foreach (var route in Routes)
        {
            var path = route.Path.Replace("{userId:guid}", Guid.NewGuid().ToString())
                .Replace("{requestId:guid}", Guid.NewGuid().ToString())
                .Replace("{notificationId:guid}", Guid.NewGuid().ToString());
            using var request = new HttpRequestMessage(new HttpMethod(route.Method), path);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
