using Fookbase.Posts.Infrastructure.Persistence;
using Fookbase.Posts.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class PostsApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("RabbitMq:ConsumerEnabled", "false");
        builder.UseSetting("Outbox:PublisherEnabled", "false");
        builder.UseSetting("MediaService:InternalToken", "integration-tests-internal-token-32-chars");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IMediaReadUrlClient>();
            services.AddSingleton<IMediaReadUrlClient, FakeMediaReadUrlClient>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PostsDbContext>();
        dbContext.Database.Migrate();

        return host;
    }
}
