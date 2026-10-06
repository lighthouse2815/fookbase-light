using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class ReelsControllerContractTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    [Fact]
    public void Reel_routes_use_controllers_and_preserve_authorization()
    {
        using var client = factory.CreateClient();
        var routes = new (string Method, string Path, bool Anonymous)[]
        {
            ("POST", "api/reels", false),
            ("GET", "api/reels", false),
            ("GET", "api/reels/{reelId:guid}", true),
            ("GET", "api/reels/{reelId:guid}/video/access", false),
            ("GET", "api/reels/{reelId:guid}/poster/access", false),
            ("POST", "api/reels/{reelId:guid}/views", false),
        };
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.Trim('/').StartsWith("api/reels") == true)
            .ToArray();

        Assert.Equal(routes.Length, endpoints.Length);
        foreach (var route in routes)
        {
            var endpoint = Assert.Single(endpoints, item =>
                item.RoutePattern.RawText!.Trim('/') == route.Path &&
                item.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(route.Method));
            Assert.NotNull(endpoint.Metadata.GetMetadata<ControllerActionDescriptor>());
            Assert.Equal(route.Anonymous, endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null);
            Assert.NotNull(endpoint.Metadata.GetMetadata<IAuthorizeData>());
        }
    }
}
