using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Modules.Posts.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Api.Modules.Posts.Config;
using Fookbase.Api.Modules.Posts.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddPostsInfrastructure(
        this IServiceCollection services,
        string connectionString,
        PostsOptions postsOptions)
    {
        postsOptions.Validate();

        services.AddDbContext<PostsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton(postsOptions);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<PostsService>();
        return services;
    }
}
