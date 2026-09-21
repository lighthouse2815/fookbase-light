using System.Net;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class ApiDocumentationEndpointsTests
{
    [Fact]
    public async Task Swagger_documentation_is_available_without_authentication()
    {
        using var factory = new IdentityApiFactory();
        using var client = factory.CreateClient();

        var document = await client.GetAsync("/swagger/v1/swagger.json");
        var ui = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, document.StatusCode);
        Assert.Equal("application/json", document.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
    }
}
