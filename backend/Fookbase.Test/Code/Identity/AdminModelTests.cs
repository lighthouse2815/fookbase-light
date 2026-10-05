using Fookbase.Api.Modules.Admin.Entities;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class AdminModelTests
{
    [Fact]
    public void Admin_mapping_matches_the_migration_snapshot()
    {
        using var db = CreateDbContext();
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData(typeof(ModerationAction), "ModeratorUser", "ModeratorUserId", typeof(User), DeleteBehavior.Restrict, true, false)]
    [InlineData(typeof(ModerationAction), "SubjectUser", "SubjectUserId", typeof(User), DeleteBehavior.Restrict, true, false)]
    [InlineData(typeof(ModerationAction), "Report", "ReportId", typeof(ContentReport), DeleteBehavior.Restrict, false, false)]
    [InlineData(typeof(UserModerationState), "User", "UserId", typeof(User), DeleteBehavior.Cascade, true, true)]
    public void Moderation_relationships_use_existing_keys_and_preserve_audit_history(
        Type entityType, string navigation, string property, Type principal,
        DeleteBehavior deleteBehavior, bool required, bool unique)
    {
        using var db = CreateDbContext();
        var entity = db.Model.FindEntityType(entityType)!;
        var mappedNavigation = entity.FindNavigation(navigation);
        Assert.NotNull(mappedNavigation);
        var foreignKey = mappedNavigation.ForeignKey;
        Assert.Equal(principal, foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(property, Assert.Single(foreignKey.Properties).Name);
        Assert.Equal(required, foreignKey.IsRequired);
        Assert.Equal(unique, foreignKey.IsUnique);
        Assert.Equal(deleteBehavior, foreignKey.DeleteBehavior);
        Assert.DoesNotContain(entity.GetProperties(), item => item.IsShadowProperty());
    }

    [Fact]
    public void Moderation_mapping_preserves_tables_lengths_and_audit_indexes()
    {
        using var db = CreateDbContext();
        var model = db.GetService<IDesignTimeModel>().Model;
        var action = model.FindEntityType(typeof(ModerationAction))!;
        Assert.Equal("ModerationActions", action.GetTableName());
        Assert.Equal(500, action.FindProperty(nameof(ModerationAction.Reason))!.GetMaxLength());
        Assert.False(action.FindProperty(nameof(ModerationAction.Reason))!.IsNullable);
        Assert.Equal(2_000, action.FindProperty(nameof(ModerationAction.InternalNote))!.GetMaxLength());
        Assert.Contains(action.GetIndexes(), index => index.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { "TargetType", "TargetId", "CreatedAtUtc" }));
        Assert.Contains(action.GetIndexes(), index => index.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { "SubjectUserId", "CreatedAtUtc" }));
        var state = model.FindEntityType(typeof(UserModerationState))!;
        Assert.Equal("UserModerationStates", state.GetTableName());
        Assert.Equal("UserId", Assert.Single(state.FindPrimaryKey()!.Properties).Name);
    }

    private static FookbaseDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<FookbaseDbContext>()
            .UseNpgsql("Host=localhost;Database=fookbase_model;Username=fookbase;Password=fookbase")
            .Options);
}
