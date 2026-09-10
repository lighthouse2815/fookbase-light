using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fookbase.Api.Modules.Messages.Data;

public sealed class MessagesDbContextFactory : IDesignTimeDbContextFactory<MessagesDbContext>
{
    public MessagesDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MessagesDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings__MessagesDatabase is required for Messages design-time operations.");
        var options = new DbContextOptionsBuilder<MessagesDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new MessagesDbContext(options);
    }
}
