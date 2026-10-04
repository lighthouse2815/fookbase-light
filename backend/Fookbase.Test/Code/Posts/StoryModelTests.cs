using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Stories.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class StoryModelTests
{
    [Fact]
    public void Story_mapping_matches_the_migration_snapshot()
    {
        using var db = CreateDbContext();
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData(typeof(Story), nameof(Story.AuthorUser), nameof(Story.AuthorUserId), typeof(User), DeleteBehavior.Restrict, false)]
    [InlineData(typeof(Story), nameof(Story.Media), nameof(Story.MediaId), typeof(MediaAsset), DeleteBehavior.Restrict, false)]
    [InlineData(typeof(StoryMediaReference), nameof(StoryMediaReference.Story), nameof(StoryMediaReference.StoryId), typeof(Story), DeleteBehavior.Cascade, true)]
    [InlineData(typeof(StoryMediaReference), nameof(StoryMediaReference.Media), nameof(StoryMediaReference.MediaId), typeof(MediaAsset), DeleteBehavior.Restrict, false)]
    [InlineData(typeof(StoryView), nameof(StoryView.Story), nameof(StoryView.StoryId), typeof(Story), DeleteBehavior.Cascade, false)]
    [InlineData(typeof(StoryView), nameof(StoryView.ViewerUser), nameof(StoryView.ViewerUserId), typeof(User), DeleteBehavior.Restrict, false)]
    [InlineData(typeof(StoryReaction), nameof(StoryReaction.Story), nameof(StoryReaction.StoryId), typeof(Story), DeleteBehavior.Cascade, false)]
    [InlineData(typeof(StoryReaction), nameof(StoryReaction.User), nameof(StoryReaction.UserId), typeof(User), DeleteBehavior.Restrict, false)]
    public void Story_relationships_use_explicit_required_keys_without_shadow_columns(
        Type entityType, string navigation, string property, Type principal, DeleteBehavior deleteBehavior, bool unique)
    {
        using var db = CreateDbContext();
        var entity = db.Model.FindEntityType(entityType)!;
        var foreignKey = entity.FindNavigation(navigation)!.ForeignKey;

        Assert.Equal(principal, foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(property, Assert.Single(foreignKey.Properties).Name);
        Assert.True(foreignKey.IsRequired);
        Assert.Equal(unique, foreignKey.IsUnique);
        Assert.Equal(deleteBehavior, foreignKey.DeleteBehavior);
        Assert.DoesNotContain(entity.GetProperties(), item => item.IsShadowProperty());
    }

    private static FookbaseDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<FookbaseDbContext>()
            .UseNpgsql("Host=localhost;Database=fookbase_model;Username=fookbase;Password=fookbase")
            .Options);
}
