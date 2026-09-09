using Fookbase.Api.Application;
using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Modules.Identity.Config;
using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Media;
using Fookbase.Api.Modules.Media.Config;
using Fookbase.Api.Modules.Posts;
using Fookbase.Api.Modules.Posts.Config;
using Fookbase.Api.Modules.Friends;
using Fookbase.Api.Modules.Users.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Api;

internal static class ModuleServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is required.");

        return services.AddIdentityInfrastructure(
            RequiredConnectionString(configuration, "IdentityDatabase"),
            jwtOptions);
    }

    public static IServiceCollection AddUsersModule(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddUsersInfrastructure(RequiredConnectionString(configuration, "UsersDatabase"));

    public static IServiceCollection AddFriendsModule(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddFriendsInfrastructure(RequiredConnectionString(configuration, "FriendsDatabase"));

    public static IServiceCollection AddPostsModule(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddPostsInfrastructure(
            RequiredConnectionString(configuration, "PostsDatabase"),
            configuration.GetSection(PostsOptions.SectionName).Get<PostsOptions>()
                ?? new PostsOptions());

    public static IServiceCollection AddMediaModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var minioOptions = configuration.GetSection(MinioOptions.SectionName).Get<MinioOptions>()
            ?? throw new InvalidOperationException("MinIO configuration is required.");

        return services.AddMediaInfrastructure(
            RequiredConnectionString(configuration, "MediaDatabase"),
            minioOptions,
            configuration.GetSection(MediaOptions.SectionName).Get<MediaOptions>()
                ?? new MediaOptions());
    }

    public static IServiceCollection AddApplicationUseCases(this IServiceCollection services)
    {
        services.AddScoped<RegistrationUseCase>();
        services.AddScoped<PostsUseCase>();

        return services;
    }

    private static string RequiredConnectionString(IConfiguration configuration, string name) =>
        configuration.GetConnectionString(name)
        ?? throw new InvalidOperationException($"Connection string '{name}' is required.");
}
