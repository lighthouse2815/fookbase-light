using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Modules.Posts.Config;

namespace Fookbase.Api.Modules.Posts;

public static class DependencyInjection
{
    public static IServiceCollection AddPostsInfrastructure(
        this IServiceCollection services,
        PostsOptions postsOptions)
    {
        postsOptions.Validate();

        services.AddSingleton(postsOptions);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<SocialInteractionsService>();
        services.AddScoped<PostsService>();
        services.AddScoped<PostsUseCase>();
        services.AddScoped<ReportsService>();
        return services;
    }
}
