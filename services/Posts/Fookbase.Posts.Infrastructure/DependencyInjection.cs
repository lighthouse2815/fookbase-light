using Fookbase.Posts.Application.Abstractions;
using Fookbase.Posts.Application.Posts;
using Fookbase.Posts.Infrastructure.IntegrationEvents;
using Fookbase.Posts.Infrastructure.Media;
using Fookbase.Posts.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Posts.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPostsInfrastructure(
        this IServiceCollection services,
        string connectionString,
        RabbitMqOptions rabbitMqOptions,
        OutboxOptions outboxOptions,
        PostsOptions postsOptions,
        MediaServiceOptions mediaServiceOptions)
    {
        rabbitMqOptions.Validate();
        outboxOptions.Validate();
        postsOptions.Validate();
        mediaServiceOptions.Validate();

        services.AddDbContext<PostsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton(rabbitMqOptions);
        services.AddSingleton(outboxOptions);
        services.AddSingleton(postsOptions);
        services.AddSingleton(mediaServiceOptions);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IPostsStore, PostsStore>();
        services.AddScoped<IPostsService, PostsService>();
        services.AddHttpClient<IMediaReadUrlClient, MediaReadUrlClient>(client =>
        {
            client.BaseAddress = new Uri(mediaServiceOptions.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        services.AddScoped<EventProjectionStore>();
        services.AddScoped<IIntegrationEventPublisher, RabbitMqIntegrationEventPublisher>();
        services.AddHostedService<ProjectionConsumer>();
        services.AddHostedService<OutboxPublisherWorker>();
        return services;
    }
}
