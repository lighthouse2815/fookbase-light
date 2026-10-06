using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Pages.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class PageModelTests
{
    [Fact]
    public void Page_mapping_matches_the_migration_snapshot()
    {
        using var db = CreateDbContext();
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Page_mapping_preserves_citext_filtered_indexes_and_composite_keys()
    {
        using var db = CreateDbContext();
        var model = db.GetService<IDesignTimeModel>().Model;
        var page = model.FindEntityType(typeof(Page))!;
        Assert.Equal("citext", page.FindProperty(nameof(Page.Username))!.GetColumnType());
        Assert.Equal(50, page.FindProperty(nameof(Page.Username))!.GetMaxLength());
        var usernameIndex = Assert.Single(page.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(Page.Username)]));
        Assert.True(usernameIndex.IsUnique);
        Assert.Equal("\"DeletedAtUtc\" IS NULL", usernameIndex.GetFilter());
        Assert.All(page.GetIndexes(), index =>
        {
            if (index.Properties.Count > 1) Assert.Equal("\"DeletedAtUtc\" IS NULL", index.GetFilter());
        });
        var invitation = model.FindEntityType(typeof(PageRoleInvitation))!;
        var pendingIndex = Assert.Single(invitation.GetIndexes(), index => index.IsUnique);
        Assert.Equal(new[] { nameof(PageRoleInvitation.PageId), nameof(PageRoleInvitation.InviteeUserId) },
            pendingIndex.Properties.Select(property => property.Name));
        Assert.Equal("\"Status\" = 0", pendingIndex.GetFilter());
        foreach (var entityType in new[] { typeof(PageMember), typeof(PageFollower), typeof(PageMediaReference) })
        {
            var key = model.FindEntityType(entityType)!.FindPrimaryKey()!;
            Assert.Equal(new[] { "PageId", entityType == typeof(PageMediaReference) ? "Slot" : "UserId" },
                key.Properties.Select(property => property.Name));
        }
    }

    [Theory]
    [InlineData(typeof(Page), nameof(Page.CreatedByUser), nameof(Page.CreatedByUserId), typeof(User), DeleteBehavior.Restrict, true)]
    [InlineData(typeof(Page), nameof(Page.AvatarMedia), nameof(Page.AvatarMediaId), typeof(MediaAsset), DeleteBehavior.Restrict, false)]
    [InlineData(typeof(Page), nameof(Page.CoverMedia), nameof(Page.CoverMediaId), typeof(MediaAsset), DeleteBehavior.Restrict, false)]
    [InlineData(typeof(PageMember), nameof(PageMember.Page), nameof(PageMember.PageId), typeof(Page), DeleteBehavior.Cascade, true)]
    [InlineData(typeof(PageMember), nameof(PageMember.User), nameof(PageMember.UserId), typeof(User), DeleteBehavior.Restrict, true)]
    [InlineData(typeof(PageFollower), nameof(PageFollower.Page), nameof(PageFollower.PageId), typeof(Page), DeleteBehavior.Cascade, true)]
    [InlineData(typeof(PageFollower), nameof(PageFollower.User), nameof(PageFollower.UserId), typeof(User), DeleteBehavior.Restrict, true)]
    [InlineData(typeof(PageRoleInvitation), nameof(PageRoleInvitation.Page), nameof(PageRoleInvitation.PageId), typeof(Page), DeleteBehavior.Cascade, true)]
    [InlineData(typeof(PageRoleInvitation), nameof(PageRoleInvitation.InviterUser), nameof(PageRoleInvitation.InviterUserId), typeof(User), DeleteBehavior.Restrict, true)]
    [InlineData(typeof(PageRoleInvitation), nameof(PageRoleInvitation.InviteeUser), nameof(PageRoleInvitation.InviteeUserId), typeof(User), DeleteBehavior.Restrict, true)]
    [InlineData(typeof(PageMediaReference), nameof(PageMediaReference.Page), nameof(PageMediaReference.PageId), typeof(Page), DeleteBehavior.Cascade, true)]
    [InlineData(typeof(PageMediaReference), nameof(PageMediaReference.Media), nameof(PageMediaReference.MediaId), typeof(MediaAsset), DeleteBehavior.Restrict, true)]
    public void Page_relationships_use_explicit_keys_and_do_not_create_shadow_columns(
        Type entityType, string navigation, string property, Type principal, DeleteBehavior deleteBehavior, bool required)
    {
        using var db = CreateDbContext();
        var entity = db.Model.FindEntityType(entityType)!;
        var foreignKey = entity.FindNavigation(navigation)!.ForeignKey;
        Assert.Equal(principal, foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(property, Assert.Single(foreignKey.Properties).Name);
        Assert.Equal(required, foreignKey.IsRequired);
        Assert.False(foreignKey.IsUnique);
        Assert.Equal(deleteBehavior, foreignKey.DeleteBehavior);
        Assert.DoesNotContain(entity.GetProperties(), item => item.IsShadowProperty());
    }

    private static FookbaseDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<FookbaseDbContext>()
            .UseNpgsql("Host=localhost;Database=fookbase_model;Username=fookbase;Password=fookbase")
            .Options);
}
