using Fookbase.Api.Modules.Friends.Entities;
using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fookbase.Friends.Api.IntegrationTests;

public sealed class FriendModelTests
{
    [Fact]
    public void Friend_mapping_matches_the_migration_snapshot()
    {
        using var db = CreateDbContext();
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Latest_migration_target_model_matches_the_cumulative_snapshot()
    {
        using var db = CreateDbContext();
        var assembly = db.GetService<IMigrationsAssembly>();
        var migration = assembly.CreateMigration(assembly.Migrations.Last().Value, db.Database.ProviderName!);
        var initializer = db.GetService<IModelRuntimeInitializer>();
        var targetModel = initializer.Initialize(migration.TargetModel, designTime: true);
        var snapshotModel = initializer.Initialize(assembly.ModelSnapshot!.Model, designTime: true);
        Assert.Empty(db.GetService<IMigrationsModelDiffer>().GetDifferences(
            targetModel.GetRelationalModel(), snapshotModel.GetRelationalModel()));
    }

    [Fact]
    public void Friend_mapping_preserves_keys_indexes_and_check_constraints()
    {
        using var db = CreateDbContext();
        var model = db.GetService<IDesignTimeModel>().Model;
        var request = model.FindEntityType(typeof(FriendRequest))!;
        Assert.Equal("UserId1", request.FindProperty(nameof(FriendRequest.User1Id))!.GetColumnName());
        Assert.Equal("UserId2", request.FindProperty(nameof(FriendRequest.User2Id))!.GetColumnName());
        var pendingPair = Assert.Single(request.GetIndexes(), index => index.IsUnique);
        Assert.Equal(new[] { "User1Id", "User2Id" }, pendingPair.Properties.Select(property => property.Name));
        Assert.Equal("UX_FriendRequests_PendingPair", pendingPair.GetDatabaseName());
        Assert.Equal("\"Status\" = 0", pendingPair.GetFilter());
        Assert.Equal("\"SenderUserId\" <> \"ReceiverUserId\"",
            request.FindCheckConstraint("CK_FriendRequests_DifferentUsers")!.Sql);
        Assert.Equal("\"UserId1\" < \"UserId2\"",
            request.FindCheckConstraint("CK_FriendRequests_CanonicalPair")!.Sql);

        var friendship = model.FindEntityType(typeof(Friendship))!;
        Assert.Equal("UserId1", friendship.FindProperty(nameof(Friendship.User1Id))!.GetColumnName());
        Assert.Equal("UserId2", friendship.FindProperty(nameof(Friendship.User2Id))!.GetColumnName());
        Assert.Equal(new[] { "User1Id", "User2Id" },
            Assert.Single(friendship.GetIndexes(), index => index.IsUnique).Properties.Select(property => property.Name));
        Assert.Equal("\"UserId1\" < \"UserId2\"",
            friendship.FindCheckConstraint("CK_Friendships_CanonicalPair")!.Sql);

        var block = model.FindEntityType(typeof(BlockedUser))!;
        Assert.Equal("BlockedUserId", block.FindProperty(nameof(BlockedUser.BlockedAccountId))!.GetColumnName());
        Assert.Equal(new[] { "BlockerUserId", "BlockedAccountId" },
            block.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal("\"BlockerUserId\" <> \"BlockedUserId\"",
            block.FindCheckConstraint("CK_BlockedUsers_DifferentUsers")!.Sql);

        var follow = model.FindEntityType(typeof(UserFollow))!;
        Assert.Equal(new[] { "FollowerUserId", "FollowingUserId" },
            follow.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal("\"FollowerUserId\" <> \"FollowingUserId\"",
            follow.FindCheckConstraint("CK_UserFollows_DifferentUsers")!.Sql);
        Assert.Contains(follow.GetIndexes(), index => index.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { "FollowerUserId", "FollowedAtUtc", "FollowingUserId" }));
        Assert.Contains(follow.GetIndexes(), index => index.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { "FollowingUserId", "FollowedAtUtc", "FollowerUserId" }));
    }

    [Theory]
    [InlineData(typeof(FriendRequest), "SenderUser", "SenderUserId", typeof(User))]
    [InlineData(typeof(FriendRequest), "ReceiverUser", "ReceiverUserId", typeof(User))]
    [InlineData(typeof(FriendRequest), "User1", "User1Id", typeof(User))]
    [InlineData(typeof(FriendRequest), "User2", "User2Id", typeof(User))]
    [InlineData(typeof(Friendship), "User1", "User1Id", typeof(User))]
    [InlineData(typeof(Friendship), "User2", "User2Id", typeof(User))]
    [InlineData(typeof(BlockedUser), "BlockerUser", "BlockerUserId", typeof(User))]
    [InlineData(typeof(BlockedUser), "BlockedAccount", "BlockedAccountId", typeof(User))]
    [InlineData(typeof(UserFollow), "FollowerUser", "FollowerUserId", typeof(User))]
    [InlineData(typeof(UserFollow), "FollowingUser", "FollowingUserId", typeof(User))]
    [InlineData(typeof(FriendNotification), "RecipientUser", "RecipientUserId", typeof(User))]
    [InlineData(typeof(FriendNotification), "ActorUser", "ActorUserId", typeof(User))]
    [InlineData(typeof(FriendNotification), "FriendRequest", "FriendRequestId", typeof(FriendRequest))]
    public void Friend_relationships_use_existing_keys_and_restrict_deletion(
        Type entityType, string navigation, string property, Type principal)
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
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.DoesNotContain(entity.GetProperties(), item => item.IsShadowProperty());
    }

    private static FookbaseDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<FookbaseDbContext>()
            .UseNpgsql("Host=localhost;Database=fookbase_model;Username=fookbase;Password=fookbase")
            .Options);
}
