using Fookbase.Api.Modules.Messages.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Messages.Data;

public sealed class MessagesDbContext(DbContextOptions<MessagesDbContext> options)
    : DbContext(options)
{
    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(MessagesDbContext).Assembly,
            type => type.Namespace?.StartsWith(
                "Fookbase.Api.Modules.Messages.Data.Configurations",
                StringComparison.Ordinal) == true);
    }
}
