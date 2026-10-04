using Fookbase.Api.Modules.Groups.Entities;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class GroupModelTests
{
    [Fact]
    public void Group_mapping_matches_the_migration_snapshot()
    {
        using var db = CreateDbContext();
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Group_mapping_preserves_filtered_indexes_lengths_and_membership_key()
    {
        using var db = CreateDbContext();
        var model = db.GetService<IDesignTimeModel>().Model;
        var group = model.FindEntityType(typeof(Group))!;
        Assert.Equal(120, group.FindProperty(nameof(Group.Name))!.GetMaxLength());
        Assert.Equal(2_000, group.FindProperty(nameof(Group.Description))!.GetMaxLength());
        Assert.All(group.GetIndexes().Where(index => index.Properties.Count == 2),
            index => Assert.Equal("\"DeletedAtUtc\" IS NULL", index.GetFilter()));
        var member = model.FindEntityType(typeof(GroupMember))!;
        Assert.Equal(new[] { "GroupId", "UserId" },
            member.FindPrimaryKey()!.Properties.Select(property => property.Name));
        foreach (var entityType in new[] { typeof(GroupInvite), typeof(GroupJoinRequest) })
        {
            var pendingIndex = Assert.Single(model.FindEntityType(entityType)!.GetIndexes(), index => index.IsUnique);
            Assert.Equal("\"Status\" = 0", pendingIndex.GetFilter());
            Assert.Equal(new[] { "GroupId", entityType == typeof(GroupInvite) ? "InviteeUserId" : "RequesterUserId" },
                pendingIndex.Properties.Select(property => property.Name));
        }
    }

    [Theory]
    [InlineData(typeof(Group), "OwnerUser", "OwnerUserId", typeof(User), DeleteBehavior.Restrict, true, false)]
    [InlineData(typeof(Group), "CoverMedia", "CoverMediaId", typeof(MediaAsset), DeleteBehavior.Restrict, false, false)]
    [InlineData(typeof(GroupMember), "Group", "GroupId", typeof(Group), DeleteBehavior.Cascade, true, false)]
    [InlineData(typeof(GroupMember), "User", "UserId", typeof(User), DeleteBehavior.Restrict, true, false)]
    [InlineData(typeof(GroupJoinRequest), "Group", "GroupId", typeof(Group), DeleteBehavior.Cascade, true, false)]
    [InlineData(typeof(GroupJoinRequest), "RequesterUser", "RequesterUserId", typeof(User), DeleteBehavior.Restrict, true, false)]
    [InlineData(typeof(GroupJoinRequest), "RespondedByUser", "RespondedByUserId", typeof(User), DeleteBehavior.Restrict, false, false)]
    [InlineData(typeof(GroupInvite), "Group", "GroupId", typeof(Group), DeleteBehavior.Cascade, true, false)]
    [InlineData(typeof(GroupInvite), "InviterUser", "InviterUserId", typeof(User), DeleteBehavior.Restrict, true, false)]
    [InlineData(typeof(GroupInvite), "InviteeUser", "InviteeUserId", typeof(User), DeleteBehavior.Restrict, true, false)]
    [InlineData(typeof(GroupRule), "Group", "GroupId", typeof(Group), DeleteBehavior.Cascade, true, false)]
    [InlineData(typeof(GroupCoverMediaReference), "Group", "GroupId", typeof(Group), DeleteBehavior.Cascade, true, true)]
    [InlineData(typeof(GroupCoverMediaReference), "Media", "MediaId", typeof(MediaAsset), DeleteBehavior.Restrict, true, false)]
    public void Group_relationships_use_existing_keys_and_explicit_delete_behavior(
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

    private static FookbaseDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<FookbaseDbContext>()
            .UseNpgsql("Host=localhost;Database=fookbase_model;Username=fookbase;Password=fookbase")
            .Options);
}
