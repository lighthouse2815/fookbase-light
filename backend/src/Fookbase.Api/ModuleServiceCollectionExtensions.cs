using Fookbase.Api.IntegrationEvents;
using Fookbase.Api.Media;
using Fookbase.Identity.Infrastructure;
using Fookbase.Identity.Infrastructure.Authentication;
using Fookbase.Media.Application.Media;
using Fookbase.Media.Infrastructure;
using Fookbase.Media.Infrastructure.Storage;
using Fookbase.Posts.Application.Abstractions;
using Fookbase.Posts.Application.Posts;
using Fookbase.Posts.Infrastructure;
using Fookbase.Users.Infrastructure;
using Fookbase.Friends.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FriendsOutboxOptions = Fookbase.Friends.Infrastructure.IntegrationEvents.OutboxOptions;
using IdentityOutboxOptions = Fookbase.Identity.Infrastructure.IntegrationEvents.OutboxOptions;
using MediaOutboxOptions = Fookbase.Media.Infrastructure.IntegrationEvents.OutboxOptions;
using PostsOutboxOptions = Fookbase.Posts.Infrastructure.IntegrationEvents.OutboxOptions;

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
            jwtOptions,
            configuration.GetSection(IdentityOutboxOptions.SectionName).Get<IdentityOutboxOptions>()
                ?? new IdentityOutboxOptions());
    }

    public static IServiceCollection AddUsersModule(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddUsersInfrastructure(RequiredConnectionString(configuration, "UsersDatabase"));

    public static IServiceCollection AddFriendsModule(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddFriendsInfrastructure(
            RequiredConnectionString(configuration, "FriendsDatabase"),
            configuration.GetSection(FriendsOutboxOptions.SectionName).Get<FriendsOutboxOptions>()
                ?? new FriendsOutboxOptions());

    public static IServiceCollection AddPostsModule(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddPostsInfrastructure(
            RequiredConnectionString(configuration, "PostsDatabase"),
            configuration.GetSection(PostsOutboxOptions.SectionName).Get<PostsOutboxOptions>()
                ?? new PostsOutboxOptions(),
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
            configuration.GetSection(MediaOutboxOptions.SectionName).Get<MediaOutboxOptions>()
                ?? new MediaOutboxOptions(),
            configuration.GetSection(MediaOptions.SectionName).Get<MediaOptions>()
                ?? new MediaOptions());
    }

    public static IServiceCollection AddInProcessModuleCommunication(this IServiceCollection services)
    {
        services.AddScoped<InProcessIntegrationEventPublisher>();
        services.AddScoped<Fookbase.Identity.Application.Abstractions.IIntegrationEventPublisher>(
            provider => provider.GetRequiredService<InProcessIntegrationEventPublisher>());
        services.AddScoped<Fookbase.Friends.Application.Abstractions.IIntegrationEventPublisher>(
            provider => provider.GetRequiredService<InProcessIntegrationEventPublisher>());
        services.AddScoped<Fookbase.Posts.Application.Abstractions.IIntegrationEventPublisher>(
            provider => provider.GetRequiredService<InProcessIntegrationEventPublisher>());
        services.AddScoped<Fookbase.Media.Application.Abstractions.IIntegrationEventPublisher>(
            provider => provider.GetRequiredService<InProcessIntegrationEventPublisher>());
        services.AddScoped<IMediaReadUrlClient, DirectMediaReadUrlClient>();

        return services;
    }

    private static string RequiredConnectionString(IConfiguration configuration, string name) =>
        configuration.GetConnectionString(name)
        ?? throw new InvalidOperationException($"Connection string '{name}' is required.");
}
