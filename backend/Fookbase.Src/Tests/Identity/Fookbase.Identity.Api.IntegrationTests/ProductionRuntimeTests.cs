using System.Text;
using Fookbase.Api.Shared.Config;
using Fookbase.Api.Shared.ErrorHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class ProductionRuntimeTests
{
    [Fact]
    public void Production_configuration_rejects_missing_explicit_origins()
    {
        var values = ProductionValues();
        values.Remove("Cors:AllowedOrigins:0");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(configuration, production: true));

        Assert.Contains("Cors:AllowedOrigins", exception.Message);
    }

    [Fact]
    public void Production_configuration_accepts_explicit_safe_baseline()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(ProductionValues()).Build();

        ProductionConfigurationValidator.Validate(configuration, production: true);
    }

    [Fact]
    public async Task Unexpected_error_response_is_safe_and_has_request_id()
    {
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "m19-request-id";
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        await handler.TryHandleAsync(context,
            new InvalidOperationException("postgres password=do-not-expose"), CancellationToken.None);

        context.Response.Body.Position = 0;
        var response = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Contains("m19-request-id", response);
        Assert.DoesNotContain("password=do-not-expose", response);
    }

    private static Dictionary<string, string?> ProductionValues() =>
        new()
        {
            ["ConnectionStrings:FookbaseDatabase"] = "Host=postgres;Database=fookbase;Username=fookbase;Password=not-logged",
            ["Jwt:Issuer"] = "Fookbase.Identity",
            ["Jwt:Audience"] = "Fookbase.Clients",
            ["Jwt:SigningKey"] = "production-test-signing-key-with-more-than-32-chars",
            ["Minio:Endpoint"] = "minio:9000",
            ["Minio:AccessKey"] = "access-key",
            ["Minio:SecretKey"] = "secret-key",
            ["Minio:BucketName"] = "fookbase-media",
            ["Cloudinary:CloudName"] = "production-test-cloud",
            ["Cloudinary:ApiKey"] = "production-test-api-key",
            ["Cloudinary:ApiSecret"] = "production-test-api-secret",
            ["DataProtection:ApplicationName"] = "Fookbase",
            ["DataProtection:KeyRingPath"] = "/var/fookbase/data-protection-keys",
            ["Cors:AllowedOrigins:0"] = "https://app.example.test",
            ["AllowedHosts"] = "api.example.test",
            ["ForwardedHeaders:Enabled"] = "false"
        };
}
