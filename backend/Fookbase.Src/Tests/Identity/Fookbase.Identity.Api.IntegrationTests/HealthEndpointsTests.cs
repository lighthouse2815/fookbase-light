using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Hosting;
using System.Net;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class HealthEndpointsTests
{
    [Fact]
    public async Task Liveness_is_available_without_dependency_checks()
    {
        using var factory = new UnreadyMinioIdentityApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var registrations = factory.Services
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value
            .Registrations;
        Assert.Contains(registrations, registration =>
            registration.Name == "postgresql" && registration.Tags.Contains("ready"));
        Assert.Contains(registrations, registration =>
            registration.Name == "minio" && registration.Tags.Contains("ready"));
    }

    [Fact]
    public async Task Readiness_fails_when_required_minio_bucket_is_unavailable()
    {
        using var factory = new UnreadyMinioIdentityApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private sealed class UnreadyMinioIdentityApiFactory : IdentityApiFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Minio:Endpoint", "127.0.0.1:1");
        }
    }
}
