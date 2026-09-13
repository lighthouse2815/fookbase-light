using Fookbase.Api.Modules.Admin;
using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Modules.Identity.Config;
using Fookbase.Api.Modules.Identity;
using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Media;
using Fookbase.Api.Modules.Media.Config;
using Fookbase.Api.Modules.Posts;
using Fookbase.Api.Modules.Posts.Config;
using Fookbase.Api.Modules.Friends;
using Fookbase.Api.Modules.Friends.Config;
using Fookbase.Api.Modules.Feed;
using Fookbase.Api.Modules.Feed.Config;
using Fookbase.Api.Modules.Groups;
using Fookbase.Api.Modules.Messages;
using Fookbase.Api.Modules.Notifications;
using Fookbase.Api.Modules.Users;
using Fookbase.Api.Modules.Reels;
using Fookbase.Api.Modules.Stories;
using Fookbase.Api.Modules.Stories.Config;
using Fookbase.Api.Modules.Pages;
using Fookbase.Api.Modules.Search;
using Fookbase.Api.Modules.Events;
using Fookbase.Api.Modules.Memories;
using Fookbase.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Api;

internal static class ModuleServiceCollectionExtensions
{
    public static IServiceCollection AddFookbasePersistence(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddDbContext<FookbaseDbContext>(options =>
            options.UseNpgsql(RequiredConnectionString(configuration, "FookbaseDatabase")));

    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is required.");
        var emailOptions = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>()
            ?? new EmailOptions();
        var adminOptions = configuration.GetSection(AdminOptions.SectionName).Get<AdminOptions>()
            ?? new AdminOptions();

        return services.AddIdentityInfrastructure(
            jwtOptions,
            emailOptions,
            adminOptions);
    }

    public static IServiceCollection AddUsersModule(
        this IServiceCollection services) =>
        services.AddUsersInfrastructure();

    public static IServiceCollection AddFriendsModule(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddFriendsInfrastructure(
            configuration.GetSection(FriendSuggestionOptions.SectionName).Get<FriendSuggestionOptions>()
                ?? new FriendSuggestionOptions());

    public static IServiceCollection AddFeedModule(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddFeedInfrastructure(
            configuration.GetSection(FeedRankingOptions.SectionName).Get<FeedRankingOptions>()
                ?? new FeedRankingOptions());

    public static IServiceCollection AddGroupsModule(
        this IServiceCollection services) =>
        services.AddGroupsInfrastructure();

    public static IServiceCollection AddPagesModule(
        this IServiceCollection services) =>
        services.AddPagesInfrastructure();

    public static IServiceCollection AddMessagesModule(
        this IServiceCollection services) =>
        services.AddMessagesInfrastructure();

    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services) =>
        services.AddNotificationsInfrastructure();

    public static IServiceCollection AddPostsModule(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddPostsInfrastructure(
            configuration.GetSection(PostsOptions.SectionName).Get<PostsOptions>()
                ?? new PostsOptions());

    public static IServiceCollection AddMediaModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var minioOptions = configuration.GetSection(MinioOptions.SectionName).Get<MinioOptions>()
            ?? throw new InvalidOperationException("MinIO configuration is required.");

        return services.AddMediaInfrastructure(
            minioOptions,
            configuration.GetSection(MediaOptions.SectionName).Get<MediaOptions>()
                ?? new MediaOptions());
    }

    public static IServiceCollection AddReelsModule(this IServiceCollection services) =>
        services.AddReelsInfrastructure();

    public static IServiceCollection AddStoriesModule(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddStoriesInfrastructure(
            configuration.GetSection(StoriesOptions.SectionName).Get<StoriesOptions>()
                ?? new StoriesOptions());

    public static IServiceCollection AddAdminModule(this IServiceCollection services) =>
        services.AddAdminInfrastructure();

    public static IServiceCollection AddSearchModule(this IServiceCollection services) =>
        services.AddSearchInfrastructure();

    public static IServiceCollection AddEventsModule(this IServiceCollection services) =>
        services.AddEventsInfrastructure();

    public static IServiceCollection AddMemoriesModule(this IServiceCollection services) =>
        services.AddMemoriesInfrastructure();

    private static string RequiredConnectionString(IConfiguration configuration, string name) =>
        configuration.GetConnectionString(name)
        ?? throw new InvalidOperationException($"Connection string '{name}' is required.");
}
