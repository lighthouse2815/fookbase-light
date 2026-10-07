using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class HealthEndpointsTests
{
    [Fact]
    public async Task Liveness_is_available_without_dependency_checks()
    {
        using var factory = new CloudinaryStatusIdentityApiFactory(false);
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
            registration.Name == "cloudinary" && registration.Tags.Contains("ready"));
    }

    [Fact]
    public async Task Readiness_fails_when_cloudinary_is_unavailable()
    {
        using var factory = new CloudinaryStatusIdentityApiFactory(false);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_succeeds_when_database_and_cloudinary_are_available()
    {
        using var factory = new CloudinaryStatusIdentityApiFactory(true);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    private sealed class CloudinaryStatusIdentityApiFactory(bool healthy) : IdentityApiFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.PostConfigure<HealthCheckServiceOptions>(options =>
            {
                var registration = options.Registrations.Single(item => item.Name == "cloudinary");
                options.Registrations.Remove(registration);
                options.Registrations.Add(new HealthCheckRegistration(
                    registration.Name, _ => new StubCloudinaryHealthCheck(healthy),
                    registration.FailureStatus, registration.Tags, registration.Timeout));
            }));
        }
    }

    private sealed class StubCloudinaryHealthCheck(bool healthy) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(healthy ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Cloudinary unavailable for this test."));
    }
}
