using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class EventControllerContractTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    [Fact]
    public async Task Event_routes_preserve_methods_and_authorization_when_registered_as_controllers()
    {
        using var client = factory.CreateClient();
        var routes = new (string Method, string Path)[]
        {
            ("GET", "api/events/mine"),
            ("GET", "api/events/upcoming"),
            ("GET", "api/events/discover"),
            ("GET", "api/events/invitations/mine"),
            ("POST", "api/events/invitations/{inviteId:guid}/accept"),
            ("POST", "api/events/invitations/{inviteId:guid}/decline"),
            ("POST", "api/events"),
            ("GET", "api/events/{id:guid}"),
            ("GET", "api/events/{id:guid}/cover"),
            ("PATCH", "api/events/{id:guid}"),
            ("DELETE", "api/events/{id:guid}"),
            ("POST", "api/events/{id:guid}/publish"),
            ("POST", "api/events/{id:guid}/cancel"),
            ("POST", "api/events/{id:guid}/rsvp"),
            ("DELETE", "api/events/{id:guid}/rsvp"),
            ("GET", "api/events/{id:guid}/participants"),
            ("POST", "api/events/{id:guid}/invites"),
            ("GET", "api/events/{id:guid}/posts"),
            ("POST", "api/events/{id:guid}/posts"),
        };
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.Trim('/').StartsWith("api/events") == true)
            .ToArray();

        Assert.Equal(routes.Length, endpoints.Length);
        foreach (var route in routes)
        {
            var endpoint = Assert.Single(endpoints, item =>
                item.RoutePattern.RawText!.Trim('/') == route.Path &&
                item.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(route.Method));
            Assert.NotNull(endpoint.Metadata.GetMetadata<ControllerActionDescriptor>());
            Assert.NotNull(endpoint.Metadata.GetMetadata<IAuthorizeData>());
            var path = "/" + route.Path.Replace("{id:guid}", Guid.NewGuid().ToString())
                .Replace("{inviteId:guid}", Guid.NewGuid().ToString());
            using var request = new HttpRequestMessage(new HttpMethod(route.Method), path);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
