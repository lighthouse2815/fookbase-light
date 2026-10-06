using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Reels.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class ReelModelTests
{
    [Fact]
    public void Reel_mapping_matches_the_migration_snapshot()
    {
        using var db = CreateDbContext();
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData("ReelPost", nameof(ReelView.ReelPostId), typeof(Post), DeleteBehavior.Cascade)]
    [InlineData("ViewerUser", nameof(ReelView.ViewerUserId), typeof(User), DeleteBehavior.Restrict)]
    public void Reel_views_map_required_relationships_without_shadow_columns(
        string navigation, string property, Type principal, DeleteBehavior deleteBehavior)
    {
        using var db = CreateDbContext();
        var entity = db.Model.FindEntityType(typeof(ReelView))!;
        var mappedNavigation = entity.FindNavigation(navigation);
        Assert.NotNull(mappedNavigation);
        var foreignKey = mappedNavigation.ForeignKey;

        Assert.Equal(principal, foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(property, Assert.Single(foreignKey.Properties).Name);
        Assert.True(foreignKey.IsRequired);
        Assert.False(foreignKey.IsUnique);
        Assert.Equal(deleteBehavior, foreignKey.DeleteBehavior);
        Assert.DoesNotContain(entity.GetProperties(), item => item.IsShadowProperty());
    }

    private static FookbaseDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<FookbaseDbContext>()
            .UseNpgsql("Host=localhost;Database=fookbase_model;Username=fookbase;Password=fookbase")
            .Options);
}
