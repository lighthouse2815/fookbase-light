using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fookbase.Media.Infrastructure.Persistence;

public sealed class MediaDbContextFactory : IDesignTimeDbContextFactory<MediaDbContext>
{
    public MediaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseNpgsql("Host=localhost;Database=media_db")
            .Options;
        return new MediaDbContext(options);
    }
}
