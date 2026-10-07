using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class DatabaseLifetimeTests
{
    [Fact]
    public async Task Disposing_the_host_disposes_its_database_connection_pool()
    {
        NpgsqlDataSource dataSource;
        await using (var factory = new IdentityApiFactory())
        {
            dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();
            await using var connection = await dataSource.OpenConnectionAsync();
            Assert.Equal(System.Data.ConnectionState.Open, connection.State);
        }

        await Assert.ThrowsAsync<ObjectDisposedException>(() => dataSource.OpenConnectionAsync().AsTask());
    }
}
