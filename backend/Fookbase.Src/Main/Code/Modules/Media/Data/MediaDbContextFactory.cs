using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fookbase.Api.Modules.Media.Data;

public sealed class MediaDbContextFactory : IDesignTimeDbContextFactory<MediaDbContext>
{
    public MediaDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MediaDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings__MediaDatabase is required for Media design-time operations.");
        }
        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new MediaDbContext(options);
    }
}
