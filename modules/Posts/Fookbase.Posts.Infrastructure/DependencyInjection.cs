using Fookbase.Posts.Application.Abstractions;
using Fookbase.Posts.Application.Posts;
using Fookbase.Posts.Infrastructure.IntegrationEvents;
using Fookbase.Posts.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Posts.Infrastructure;

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
