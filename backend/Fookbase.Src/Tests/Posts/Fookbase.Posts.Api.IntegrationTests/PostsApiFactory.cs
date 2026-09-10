using Fookbase.Api.Modules.Friends.Data;
using Fookbase.Api.Modules.Media.Data;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Posts.Data;
using Fookbase.Api.Modules.Identity.Data;
using Fookbase.Api.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fookbase.Posts.Api.IntegrationTests;

public class PostsApiFactory : WebApplicationFactory<Program>
{
    private readonly string? connectionStringOverride;

    public PostsApiFactory()
    {
    }

    protected PostsApiFactory(string connectionStringOverride)
    {
        this.connectionStringOverride = connectionStringOverride;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = connectionStringOverride
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__FookbaseDatabase")
            ?? throw new InvalidOperationException("Fookbase development database connection string is required.");
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:FookbaseDatabase", connectionString);
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-must-have-32-characters");
        builder.UseSetting("Minio:AccessKey", "integration-tests");
        builder.UseSetting("Minio:SecretKey", "integration-tests");
        builder.UseSetting("Minio:BucketInitializationEnabled", "false");
        builder.UseSetting("Media:CleanupIntervalSeconds", "3600");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IObjectStorage>();
            services.AddSingleton<IObjectStorage, FakeObjectStorage>();
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
