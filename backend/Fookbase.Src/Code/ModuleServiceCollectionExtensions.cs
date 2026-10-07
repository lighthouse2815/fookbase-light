using Fookbase.Api.Modules.Admin;
using Fookbase.Api.Modules.Identity.Config;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity;
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
using Fookbase.Api.Modules.Ai;
using Fookbase.Api.Modules.Ai.Config;
using Fookbase.Api.Modules.Notifications.Config;
using Fookbase.Api.Modules.Games;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fookbase.Api;

internal static class ModuleServiceCollectionExtensions
{
    public static IServiceCollection AddFookbasePersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = RequiredConnectionString(configuration, "FookbaseDatabase");
        var commandTimeoutSeconds = configuration.GetValue("Database:CommandTimeoutSeconds", 30);
        if (commandTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Database:CommandTimeoutSeconds must be positive.");
        }

        services.AddSingleton(_ => new NpgsqlDataSourceBuilder(connectionString).Build());
        services.AddDbContext<FookbaseDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>(),
                npgsql => npgsql.CommandTimeout(commandTimeoutSeconds)));
        return services;
    }

    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(IdentityModuleConstants.ConfigurationSections.Jwt).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is required.");
        var emailOptions = configuration.GetSection(IdentityModuleConstants.ConfigurationSections.Email).Get<EmailOptions>()
            ?? new EmailOptions();
        var googleAuthenticationOptions = configuration
            .GetSection(IdentityModuleConstants.ConfigurationSections.GoogleAuthentication)
            .Get<GoogleAuthenticationOptions>() ?? new GoogleAuthenticationOptions();
        var smsOptions = configuration.GetSection(IdentityModuleConstants.ConfigurationSections.Sms).Get<SmsOptions>()
            ?? new SmsOptions();

        return services.AddIdentityInfrastructure(
            jwtOptions,
            emailOptions,
            googleAuthenticationOptions,
            smsOptions);
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
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddNotificationsInfrastructure(
            configuration.GetSection(PushNotificationOptions.SectionName).Get<PushNotificationOptions>()
                ?? new PushNotificationOptions());

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
        var cloudinaryOptions = configuration.GetSection(CloudinaryOptions.SectionName).Get<CloudinaryOptions>()
            ?? throw new InvalidOperationException("Cloudinary configuration is required.");

        return services.AddMediaInfrastructure(
            cloudinaryOptions,
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

    public static IServiceCollection AddAiModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection(AiChatOptions.SectionName).Get<AiChatOptions>()
            ?? new AiChatOptions();
        options.Validate();
        return services.AddAiInfrastructure(options);
    }

    public static IServiceCollection AddGamesModule(this IServiceCollection services) =>
        services.AddGamesInfrastructure();

    private static string RequiredConnectionString(IConfiguration configuration, string name) =>
        configuration.GetConnectionString(name)
        ?? throw new InvalidOperationException($"Connection string '{name}' is required.");
}
