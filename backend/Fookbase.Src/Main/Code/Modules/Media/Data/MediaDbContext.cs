using Fookbase.Api.Modules.Media.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Media.Data;

public sealed class MediaDbContext(DbContextOptions<MediaDbContext> options) : DbContext(options)
{
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<MediaReference> MediaReferences => Set<MediaReference>();
    public DbSet<ProfileMediaReference> ProfileMediaReferences => Set<ProfileMediaReference>();
    public DbSet<ObjectDeletion> ObjectDeletions => Set<ObjectDeletion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(MediaDbContext).Assembly,
            type => type.Namespace?.StartsWith(
                "Fookbase.Api.Modules.Media.Data.Configurations",
                StringComparison.Ordinal) == true);
    }
}
