using Microsoft.EntityFrameworkCore;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class PhotoModelTests
{
    [Fact]
    public void Photo_mapping_matches_the_migration_snapshot()
    {
        using var db = new FookbaseDbContext(new DbContextOptionsBuilder<FookbaseDbContext>()
            .UseNpgsql("Host=localhost;Database=fookbase_model;Username=fookbase;Password=fookbase")
            .Options);

        Assert.False(db.Database.HasPendingModelChanges());
    }
}
