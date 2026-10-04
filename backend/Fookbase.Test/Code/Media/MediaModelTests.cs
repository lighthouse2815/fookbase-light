using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Fookbase.Media.Api.IntegrationTests;

public sealed class MediaModelTests
{
    [Fact]
    public void Media_mapping_matches_the_migration_snapshot()
    {
        using var db = CreateDbContext();
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Media_mapping_preserves_keys_indexes_lengths_and_enum_storage()
    {
        using var db = CreateDbContext();
        var model = db.GetService<IDesignTimeModel>().Model;
        var media = model.FindEntityType(typeof(MediaAsset))!;
        Assert.Equal("MediaAssets", media.GetTableName());
        Assert.DoesNotContain(media.GetForeignKeys(), key => key.Properties.Any(property => property.Name == "OwnerUserId"));
        foreach (var property in new[] { "ObjectKey", "ProcessedObjectKey", "PosterObjectKey" })
            Assert.Equal(256, media.FindProperty(property)!.GetMaxLength());
        Assert.Equal(255, media.FindProperty("OriginalFileName")!.GetMaxLength());
        Assert.Equal(100, media.FindProperty("ContentType")!.GetMaxLength());
        Assert.Equal(1000, media.FindProperty("ProcessingError")!.GetMaxLength());
        foreach (var property in new[] { "ObjectKey", "OriginalFileName", "ContentType" })
            Assert.False(media.FindProperty(property)!.IsNullable);
        Assert.Equal("integer", media.FindProperty("MediaType")!.GetColumnType());
        Assert.Equal("integer", media.FindProperty("Status")!.GetColumnType());
        AssertIndex(media, ["ObjectKey"], unique: true);
        AssertIndex(media, ["OwnerUserId", "CreatedAtUtc"]);
        AssertIndex(media, ["Status", "UploadExpiresAtUtc"]);
        AssertIndex(media, ["Status", "CreatedAtUtc"], filter: "\"Status\" = 4");

        var processing = model.FindEntityType(typeof(MediaProcessingJob))!;
        Assert.Equal(2000, processing.FindProperty("LastError")!.GetMaxLength());
        Assert.Equal("integer", processing.FindProperty("Status")!.GetColumnType());
        AssertIndex(processing, ["MediaId"], unique: true);
        AssertIndex(processing, ["Status", "NextAttemptAtUtc", "CreatedAtUtc"]);

        var reference = model.FindEntityType(typeof(MediaReference))!;
        Assert.Equal(new[] { "MediaId", "PostId" }, reference.FindPrimaryKey()!.Properties.Select(p => p.Name));
        AssertIndex(reference, ["PostId"]);

        var profile = model.FindEntityType(typeof(ProfileMediaReference))!;
        Assert.Equal(new[] { "UserId", "Slot" }, profile.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.Equal("integer", profile.FindProperty("Slot")!.GetColumnType());
        AssertIndex(profile, ["MediaId"]);

        var deletion = model.FindEntityType(typeof(ObjectDeletion))!;
        Assert.Equal(256, deletion.FindProperty("ObjectKey")!.GetMaxLength());
        Assert.False(deletion.FindProperty("ObjectKey")!.IsNullable);
        Assert.Equal(2000, deletion.FindProperty("LastError")!.GetMaxLength());
        AssertIndex(deletion, ["ProcessedAtUtc", "FailedAtUtc", "NextAttemptAtUtc", "CreatedAtUtc"]);
    }

    [Theory]
    [InlineData(typeof(MediaProcessingJob), "Media", "MediaId", typeof(MediaAsset), DeleteBehavior.Cascade, true, "ProcessingJob")]
    [InlineData(typeof(MediaReference), "Media", "MediaId", typeof(MediaAsset), DeleteBehavior.Restrict, false, "PostReferences")]
    [InlineData(typeof(MediaReference), "Post", "PostId", typeof(Post), DeleteBehavior.Cascade, false, null)]
    [InlineData(typeof(ProfileMediaReference), "User", "UserId", typeof(User), DeleteBehavior.Cascade, false, null)]
    [InlineData(typeof(ProfileMediaReference), "Media", "MediaId", typeof(MediaAsset), DeleteBehavior.Restrict, false, "ProfileReferences")]
    [InlineData(typeof(ObjectDeletion), "Media", "MediaId", typeof(MediaAsset), DeleteBehavior.Restrict, false, "ObjectDeletions")]
    public void Media_relationships_use_explicit_keys_without_shadow_columns(
        Type entityType, string navigation, string property, Type principal, DeleteBehavior behavior, bool unique, string? inverse)
    {
        using var db = CreateDbContext();
        var entity = db.Model.FindEntityType(entityType)!;
        var relationship = entity.FindNavigation(navigation);
        Assert.NotNull(relationship);
        var foreignKey = relationship.ForeignKey;
        Assert.Equal(principal, foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(property, Assert.Single(foreignKey.Properties).Name);
        Assert.True(foreignKey.IsRequired);
        Assert.Equal(behavior, foreignKey.DeleteBehavior);
        Assert.Equal(unique, foreignKey.IsUnique);
        Assert.Equal(inverse, foreignKey.PrincipalToDependent?.Name);
        Assert.DoesNotContain(entity.GetProperties(), item => item.IsShadowProperty());
    }

    private static void AssertIndex(IEntityType entity, string[] properties, bool unique = false, string? filter = null)
    {
        var index = Assert.Single(entity.GetIndexes(), item => item.Properties.Select(p => p.Name).SequenceEqual(properties));
        Assert.Equal(unique, index.IsUnique);
        Assert.Equal(filter, index.GetFilter());
    }

    private static FookbaseDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<FookbaseDbContext>()
            .UseNpgsql("Host=localhost;Database=fookbase_model;Username=fookbase;Password=fookbase")
            .Options);
}
