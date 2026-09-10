using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fookbase.Api.Persistence;

public sealed class FookbaseDbContextFactory : IDesignTimeDbContextFactory<FookbaseDbContext>
{
    public FookbaseDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__FookbaseDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings__FookbaseDatabase is required for Fookbase design-time operations.");
        }

        var options = new DbContextOptionsBuilder<FookbaseDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new FookbaseDbContext(options);
    }
}
