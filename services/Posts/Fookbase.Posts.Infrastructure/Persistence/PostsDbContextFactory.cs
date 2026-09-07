using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fookbase.Posts.Infrastructure.Persistence;

public sealed class PostsDbContextFactory : IDesignTimeDbContextFactory<PostsDbContext>
{
    public PostsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PostsDbContext>()
            .UseNpgsql("Host=localhost;Database=posts_db")
            .Options;
        return new PostsDbContext(options);
    }
}
