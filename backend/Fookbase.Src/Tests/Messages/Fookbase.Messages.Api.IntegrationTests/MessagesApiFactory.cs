using Fookbase.Api.Modules.Messages.Data;
using Fookbase.Api.Modules.Friends.Data;
using Fookbase.Api.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Fookbase.Messages.Api.IntegrationTests;

public sealed class MessagesApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__FookbaseDatabase")
            ?? throw new InvalidOperationException("Fookbase development database connection string is required.");
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Issuer", "Fookbase.Tests");
        builder.UseSetting("Jwt:Audience", "Fookbase.Tests.Clients");
        builder.UseSetting("Jwt:SigningKey", "messages-integration-tests-signing-key-12345");
        builder.UseSetting("ConnectionStrings:FookbaseDatabase", connectionString);
        builder.UseSetting("Minio:AccessKey", "integration-tests");
        builder.UseSetting("Minio:SecretKey", "integration-tests");
        builder.UseSetting("Minio:BucketInitializationEnabled", "false");
        builder.UseSetting("Cloudinary:CloudName", "integration-tests");
        builder.UseSetting("Cloudinary:ApiKey", "test-api-key");
        builder.UseSetting("Cloudinary:ApiSecret", "test-api-secret");
        builder.UseSetting("Media:CleanupIntervalSeconds", "3600");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<FookbaseDbContext>().Database.Migrate();

        return host;
    }
}
