using Fookbase.Api.Modules.Messages.Data;
using Fookbase.Api.Modules.Friends.Data;
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
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MessagesDatabase")
            ?? throw new InvalidOperationException("Messages development database connection string is required.");
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Issuer", "Fookbase.Tests");
        builder.UseSetting("Jwt:Audience", "Fookbase.Tests.Clients");
        builder.UseSetting("Jwt:SigningKey", "messages-integration-tests-signing-key-12345");
        foreach (var module in new[] { "Identity", "Users", "Friends", "Messages", "Posts", "Media" })
        {
            builder.UseSetting($"ConnectionStrings:{module}Database", connectionString);
        }
        builder.UseSetting("Minio:AccessKey", "integration-tests");
        builder.UseSetting("Minio:SecretKey", "integration-tests");
        builder.UseSetting("Minio:BucketInitializationEnabled", "false");
        builder.UseSetting("Media:CleanupIntervalSeconds", "3600");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<MessagesDbContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<FriendsDbContext>().Database.Migrate();

        return host;
    }
}
