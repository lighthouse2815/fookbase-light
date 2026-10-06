using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Photos.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class PhotoModelTests
{
    [Fact]
    public void Photo_mapping_matches_the_migration_snapshot()
    {
        using var db = CreateDbContext();
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData(typeof(PhotoAlbum), "OwnerUser", "OwnerUserId", typeof(User), DeleteBehavior.Restrict)]
    [InlineData(typeof(AlbumMedia), "Album", "AlbumId", typeof(PhotoAlbum), DeleteBehavior.Cascade)]
    [InlineData(typeof(AlbumMedia), "Media", "MediaId", typeof(MediaAsset), DeleteBehavior.Restrict)]
    public void Photo_relationships_use_existing_ids_without_shadow_columns(
        Type entityType, string navigation, string property, Type principal, DeleteBehavior deleteBehavior)
    {
        using var db = CreateDbContext();
        var entity = db.Model.FindEntityType(entityType)!;
        var mappedNavigation = entity.FindNavigation(navigation);
        Assert.NotNull(mappedNavigation);
        var foreignKey = mappedNavigation.ForeignKey;
        Assert.Equal(principal, foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(property, Assert.Single(foreignKey.Properties).Name);
        Assert.True(foreignKey.IsRequired);
        Assert.False(foreignKey.IsUnique);
        Assert.Equal(deleteBehavior, foreignKey.DeleteBehavior);
        Assert.DoesNotContain(entity.GetProperties(), item => item.IsShadowProperty());
        if (entityType == typeof(AlbumMedia) && navigation == "Album")
        {
            Assert.Equal("MediaItems", mappedNavigation.Inverse!.Name);
            Assert.True(mappedNavigation.Inverse.IsCollection);
        }
    }

    private static FookbaseDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<FookbaseDbContext>()
            .UseNpgsql("Host=localhost;Database=fookbase_model;Username=fookbase;Password=fookbase")
            .Options);
}
