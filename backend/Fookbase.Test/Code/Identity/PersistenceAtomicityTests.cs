using System.Net;
using System.Net.Http.Json;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Users.Entities;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Shared.ErrorHandling;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class PersistenceAtomicityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Business_failure_after_user_creation_rolls_back_registration(bool google)
    {
        await using var database = await TemporaryFookbaseDatabase.CreateAsync();
        using var factory = new IsolatedIdentityApiFactory(database.ConnectionString);
        using var failingFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<FookbaseDbContext>(options => options.AddInterceptors(new RejectProfileInsert()))));
        using var client = failingFactory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..16];

        HttpResponseMessage response;
        if (google)
        {
            client.DefaultRequestHeaders.Add("X-Test-Google-Sub", suffix);
            client.DefaultRequestHeaders.Add("X-Test-Google-Email", $"business-{suffix}@example.com");
            client.DefaultRequestHeaders.Add("X-Test-Google-Email-Verified", "true");
            response = await client.GetAsync("/api/auth/google/callback?client=web");
        }
        else
        {
            response = await client.PostAsJsonAsync("/api/auth/register",
                new RegisterRequest($"business-{suffix}@example.com", $"business_{suffix}", "Password123!"));
        }

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("profile_rejected", problem.RootElement.GetProperty("error").GetProperty("code").GetString());
        await using var db = CreateDbContext(database.ConnectionString);
        Assert.False(await db.Users.AnyAsync());
        Assert.False(await db.AuthSessions.AnyAsync());
        Assert.False(await db.RefreshTokens.AnyAsync());
        Assert.False(await db.UserProfiles.AnyAsync());
        Assert.False(await db.UserPrivacySettings.AnyAsync());
        Assert.False(await db.UserLogins.AnyAsync());
        Assert.False(await db.ExternalLoginTickets.AnyAsync());
    }

    private sealed class RejectProfileInsert : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<UserProfile>().Any(entry => entry.State == EntityState.Added))
            {
                throw new BusinessException(new ApplicationError(
                    "profile_rejected", "Profile creation was rejected.", ApplicationErrorType.Conflict));
            }

            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task Fresh_database_applies_the_complete_fookbase_migration()
    {
        await using var database = await TemporaryFookbaseDatabase.CreateAsync();
        await using var dbContext = CreateDbContext(database.ConnectionString);

        await dbContext.Database.MigrateAsync();

        Assert.Empty(await dbContext.Database.GetPendingMigrationsAsync());
        Assert.True(await dbContext.Database.CanConnectAsync());
        Assert.NotEmpty(await dbContext.Database.GetAppliedMigrationsAsync());
        Assert.Contains(
            dbContext.Model.GetEntityTypes(),
            entity => entity.ClrType.Name == "Message");
        Assert.Contains(
            dbContext.Model.GetEntityTypes(),
            entity => entity.ClrType.Name == "MediaAsset");
    }

    [Fact]
    public async Task Failed_profile_creation_rolls_back_the_identity_user_and_refresh_token()
    {
        await using var database = await TemporaryFookbaseDatabase.CreateAsync();
        using var factory = new IsolatedIdentityApiFactory(database.ConnectionString);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE FUNCTION fail_profile_insert() RETURNS trigger AS $$
            BEGIN
                RAISE EXCEPTION 'profile insert intentionally rejected';
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER reject_user_profile_insert
            BEFORE INSERT ON "UserProfiles"
            FOR EACH ROW EXECUTE FUNCTION fail_profile_insert();
            """);

        var suffix = Guid.NewGuid().ToString("N")[..16];
        var email = $"atomic-{suffix}@example.com";
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, $"atomic_{suffix}", "Password123!"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        dbContext.ChangeTracker.Clear();
        Assert.False(await dbContext.Users.AnyAsync(user => user.Email == email));
        Assert.False(await dbContext.RefreshTokens.AnyAsync());
        Assert.False(await dbContext.UserProfiles.AnyAsync());
    }

    private static FookbaseDbContext CreateDbContext(string connectionString) =>
        new(new DbContextOptionsBuilder<FookbaseDbContext>()
            .UseNpgsql(connectionString)
            .Options);

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
            var databaseName = $"fookbase_migration_{Guid.NewGuid():N}";
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

internal sealed class IsolatedIdentityApiFactory(string connectionString)
    : IdentityApiFactory(connectionString);
