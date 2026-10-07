using Fookbase.Api.Modules.Media.Abstractions;
using Fookbase.Api.Modules.Media.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Fookbase.Media.Api.IntegrationTests;

public sealed class MediaApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var mediaConnectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__FookbaseDatabase")
            ?? throw new InvalidOperationException(
                "Fookbase development database connection string is required.");

        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:FookbaseDatabase", mediaConnectionString);
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-must-have-32-characters");
        builder.UseSetting("Cloudinary:CloudName", "integration-tests");
        builder.UseSetting("Cloudinary:ApiKey", "test-api-key");
        builder.UseSetting("Cloudinary:ApiSecret", "test-api-secret");
        builder.UseSetting("Media:CleanupIntervalSeconds", "3600");
        builder.UseSetting("Media:VideoProcessingIntervalSeconds", "1");
        builder.UseSetting("Media:VideoProcessingRetryDelaySeconds", "1");
        builder.UseSetting("Media:VideoProcessingEnabled", "true");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IObjectStorage>();
            services.AddSingleton<InMemoryObjectStorage>();
            services.AddSingleton<IObjectStorage>(provider =>
                provider.GetRequiredService<InMemoryObjectStorage>());
            services.RemoveAll<IVideoProcessor>();
            services.AddSingleton<IVideoProcessor, FakeVideoProcessor>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        dbContext.Database.Migrate();
        return host;
    }
}
