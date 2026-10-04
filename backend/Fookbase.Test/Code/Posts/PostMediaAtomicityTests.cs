using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Notifications.Entities;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Posts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class PostMediaAtomicityTests
{
    [Fact]
    public async Task Failed_media_reference_write_rolls_back_the_post_and_post_media()
    {
        await using var database = await TemporaryFookbaseDatabase.CreateAsync();
        using var factory = new IsolatedPostsApiFactory(database.ConnectionString);
        _ = factory.Services;
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var authorUserId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var media = MediaAsset.CreatePending(
            mediaId,
            authorUserId,
            MediaType.IMAGE,
            $"{authorUserId:N}/{mediaId:N}.png",
            "photo.png",
            "image/png",
            11,
            now,
            now.AddMinutes(5));
        media.MarkReady(11, now);
        dbContext.MediaAssets.Add(media);
        await dbContext.SaveChangesAsync();
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE FUNCTION fail_media_reference_insert() RETURNS trigger AS $$
            BEGIN
                RAISE EXCEPTION 'media reference insert intentionally rejected';
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER reject_media_reference_insert
            BEFORE INSERT ON "MediaReferences"
            FOR EACH ROW EXECUTE FUNCTION fail_media_reference_insert();
            """);

        var posts = scope.ServiceProvider.GetRequiredService<PostsUseCase>();

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            posts.CreatePostAsync(authorUserId, "atomic post", "public", [mediaId]));

        dbContext.ChangeTracker.Clear();
        Assert.False(await dbContext.Posts.AnyAsync(post => post.AuthorUserId == authorUserId));
        Assert.False(await dbContext.PostMedia.AnyAsync());
        Assert.False(await dbContext.MediaReferences.AnyAsync());
    }

    [Fact]
    public async Task Failed_notification_write_rolls_back_the_reaction()
    {
        await using var database = await TemporaryFookbaseDatabase.CreateAsync();
        using var factory = new IsolatedPostsApiFactory(database.ConnectionString);
        _ = factory.Services;
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var authorUserId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var post = Post.Create(
            Guid.NewGuid(),
            authorUserId,
            "atomic notification",
            PostPrivacy.PUBLIC,
            DateTimeOffset.UtcNow);
        dbContext.Posts.Add(post);
        await dbContext.SaveChangesAsync();
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE FUNCTION fail_notification_insert() RETURNS trigger AS $$
            BEGIN
                RAISE EXCEPTION 'notification insert intentionally rejected';
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER reject_notification_insert
            BEFORE INSERT ON "Notifications"
            FOR EACH ROW EXECUTE FUNCTION fail_notification_insert();
            """);

        var posts = scope.ServiceProvider.GetRequiredService<PostsUseCase>();

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            posts.SetReactionAsync(actorUserId, post.Id, "like"));

        dbContext.ChangeTracker.Clear();
        Assert.False(await dbContext.PostReactions.AnyAsync(reaction =>
            reaction.PostId == post.Id && reaction.UserId == actorUserId));
        Assert.False(await dbContext.Notifications.AnyAsync(notification =>
            notification.Type == NotificationType.POST_REACTION && notification.EntityId == post.Id));
    }

    private sealed class TemporaryFookbaseDatabase(
        string connectionString,
        string administrativeConnectionString,
        string databaseName) : IAsyncDisposable
    {
        public string ConnectionString => connectionString;

        public static async Task<TemporaryFookbaseDatabase> CreateAsync()
        {
            var templateConnectionString = Environment.GetEnvironmentVariable(
                "ConnectionStrings__FookbaseDatabase")
                ?? throw new InvalidOperationException(
                    "Fookbase development database connection string is required.");
            var databaseName = $"fookbase_post_atomic_{Guid.NewGuid():N}";
            var databaseConnection = new NpgsqlConnectionStringBuilder(templateConnectionString)
            {
                Database = databaseName
            }.ConnectionString;
            var administrativeConnection = new NpgsqlConnectionStringBuilder(templateConnectionString)
            {
                Database = "postgres"
            }.ConnectionString;

            await using var connection = new NpgsqlConnection(administrativeConnection);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                $"CREATE DATABASE \"{databaseName}\"",
                connection);
            await command.ExecuteNonQueryAsync();

            return new TemporaryFookbaseDatabase(
                databaseConnection,
                administrativeConnection,
                databaseName);
        }

        public async ValueTask DisposeAsync()
        {
            await using var connection = new NpgsqlConnection(administrativeConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)",
                connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}

internal sealed class IsolatedPostsApiFactory(string connectionString)
    : PostsApiFactory(connectionString);
