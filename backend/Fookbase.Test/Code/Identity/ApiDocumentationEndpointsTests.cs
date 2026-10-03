using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class ApiDocumentationEndpointsTests
{
    [Theory]
    [InlineData("Testing")]
    [InlineData("Development")]
    public async Task Swagger_documentation_is_available_without_authentication(string environment)
    {
        using var factory = new IdentityApiFactory();
        using var environmentFactory = factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment));
        using var client = environmentFactory.CreateClient();

        using var document = await client.GetAsync("/swagger/v1/swagger.json");
        using var ui = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, document.StatusCode);
        Assert.Equal("application/json", document.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);

        using var body = JsonDocument.Parse(await document.Content.ReadAsStringAsync());
        var paths = body.RootElement.GetProperty("paths");
        Assert.False(paths.TryGetProperty("/api/auth/register", out _));
        Assert.True(paths.TryGetProperty("/api/auth/registration/start", out _));
        Assert.True(paths.TryGetProperty("/api/auth/registration/resend", out _));
        Assert.True(paths.TryGetProperty("/api/auth/registration/verify", out _));
    }
}
