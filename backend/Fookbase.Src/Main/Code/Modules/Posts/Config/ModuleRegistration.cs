using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Modules.Posts.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Api.Modules.Posts.Config;

public static class DependencyInjection
{
    public static IServiceCollection AddPostsInfrastructure(
        this IServiceCollection services,
        string connectionString,
        OutboxOptions outboxOptions,
        PostsOptions postsOptions)
    {
        outboxOptions.Validate();
        postsOptions.Validate();

        services.AddDbContext<PostsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton(outboxOptions);
        services.AddSingleton(postsOptions);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IPostsStore, PostsStore>();
        services.AddScoped<IPostsService, PostsService>();
        services.AddScoped<EventProjectionStore>();
        services.AddHostedService<OutboxPublisherWorker>();
        return services;
    }
}
