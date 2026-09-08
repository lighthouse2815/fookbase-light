using Fookbase.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Media.Infrastructure.Persistence;

public sealed class MediaDbContext(DbContextOptions<MediaDbContext> options) : DbContext(options)
{
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<KnownUser> KnownUsers => Set<KnownUser>();
    public DbSet<MediaReference> MediaReferences => Set<MediaReference>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ObjectDeletion> ObjectDeletions => Set<ObjectDeletion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MediaDbContext).Assembly);
    }
}
