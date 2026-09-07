using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fookbase.Posts.Infrastructure.Persistence;

public sealed class PostsDbContextFactory : IDesignTimeDbContextFactory<PostsDbContext>
{
    public PostsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PostsDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings__PostsDatabase is required for Posts design-time operations.");
        var options = new DbContextOptionsBuilder<PostsDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new PostsDbContext(options);
    }
}
