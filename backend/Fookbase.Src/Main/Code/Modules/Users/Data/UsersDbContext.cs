using Fookbase.Api.Modules.Users.Models;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Users.Data;

public sealed class UsersDbContext(DbContextOptions<UsersDbContext> options)
    : DbContext(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(UsersDbContext).Assembly,
            type => type.Namespace?.StartsWith(
                "Fookbase.Api.Modules.Users.Data",
                StringComparison.Ordinal) == true);
    }
}
