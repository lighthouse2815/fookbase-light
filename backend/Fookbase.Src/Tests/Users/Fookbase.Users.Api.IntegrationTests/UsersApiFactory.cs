using Fookbase.Api.Modules.Users.Data;
using Fookbase.Api.Modules.Media.Data;
using Fookbase.Api.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Fookbase.Users.Api.IntegrationTests;

public sealed class UsersApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__FookbaseDatabase")
            ?? throw new InvalidOperationException("Fookbase development database connection string is required.");
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:FookbaseDatabase", connectionString);
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-must-have-32-characters");
        builder.UseSetting("Minio:AccessKey", "integration-tests");
        builder.UseSetting("Minio:SecretKey", "integration-tests");
        builder.UseSetting("Minio:BucketInitializationEnabled", "false");
        builder.UseSetting("Media:CleanupIntervalSeconds", "3600");
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
