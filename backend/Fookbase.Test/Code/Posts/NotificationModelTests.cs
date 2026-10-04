using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Notifications.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class NotificationModelTests
{
    [Fact]
    public void Notification_mapping_matches_the_migration_snapshot()
    {
        using var db = CreateDbContext();
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Notification_mapping_preserves_indexes_lengths_and_enum_storage()
    {
        using var db = CreateDbContext();
        var model = db.GetService<IDesignTimeModel>().Model;
        var notification = model.FindEntityType(typeof(Notification))!;
        Assert.Equal("Notifications", notification.GetTableName());
        foreach (var property in new[] { nameof(Notification.Type), nameof(Notification.EntityType) })
        {
            Assert.Equal("integer", notification.FindProperty(property)!.GetColumnType());
        }
        AssertIndex(notification, ["RecipientUserId", "CreatedAtUtc", "Id"]);
        AssertIndex(notification, ["RecipientUserId", "IsRead", "CreatedAtUtc"]);
        AssertIndex(notification, ["RecipientUserId", "ActorUserId", "Type", "EntityType", "EntityId"]);

        var device = model.FindEntityType(typeof(PushDevice))!;
        Assert.Equal(255, device.FindProperty(nameof(PushDevice.ExpoPushToken))!.GetMaxLength());
        AssertIndex(device, ["ExpoPushToken"], unique: true);
        AssertIndex(device, ["UserId", "DisabledAtUtc"]);

        var receipt = model.FindEntityType(typeof(PushDeliveryReceipt))!;
        Assert.Equal(64, receipt.FindProperty(nameof(PushDeliveryReceipt.ExpoReceiptId))!.GetMaxLength());
        AssertIndex(receipt, ["ExpoReceiptId"], unique: true);
        AssertIndex(receipt, ["CheckedAtUtc", "AvailableAtUtc"]);
    }

    [Theory]
    [InlineData(typeof(Notification), "RecipientUser", "RecipientUserId", typeof(User), DeleteBehavior.Restrict, true)]
    [InlineData(typeof(Notification), "ActorUser", "ActorUserId", typeof(User), DeleteBehavior.Restrict, false)]
    [InlineData(typeof(PushDevice), "User", "UserId", typeof(User), DeleteBehavior.Restrict, true)]
    [InlineData(typeof(PushDeliveryReceipt), "PushDevice", "PushDeviceId", typeof(PushDevice), DeleteBehavior.Cascade, true)]
    public void Notification_relationships_use_explicit_keys_without_shadow_columns(
        Type entityType, string navigation, string property, Type principal, DeleteBehavior deleteBehavior, bool required)
    {
        using var db = CreateDbContext();
        var entity = db.Model.FindEntityType(entityType)!;
        var relationship = entity.FindNavigation(navigation);
        Assert.NotNull(relationship);
        var foreignKey = relationship.ForeignKey;
        Assert.Equal(principal, foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(property, Assert.Single(foreignKey.Properties).Name);
        Assert.Equal(required, foreignKey.IsRequired);
        Assert.Equal(deleteBehavior, foreignKey.DeleteBehavior);
        Assert.False(foreignKey.IsUnique);
        Assert.DoesNotContain(entity.GetProperties(), item => item.IsShadowProperty());
        if (entityType == typeof(PushDeliveryReceipt))
        {
            Assert.Equal("DeliveryReceipts", foreignKey.PrincipalToDependent!.Name);
        }
    }

    private static void AssertIndex(IEntityType entity, string[] properties, bool unique = false)
    {
        var index = Assert.Single(entity.GetIndexes(), item =>
            item.Properties.Select(property => property.Name).SequenceEqual(properties));
        Assert.Equal(unique, index.IsUnique);
        Assert.Null(index.GetFilter());
    }

    private static FookbaseDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<FookbaseDbContext>()
            .UseNpgsql("Host=localhost;Database=fookbase_model;Username=fookbase;Password=fookbase")
            .Options);
}
