using Fookbase.Api.Modules.Events.Entities;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class EventModelTests
{
    [Fact]
    public void Event_mapping_matches_the_migration_snapshot()
    {
        using var db = CreateDbContext();
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Event_mapping_preserves_lengths_filtered_indexes_and_participant_key()
    {
        using var db = CreateDbContext();
        var model = db.GetService<IDesignTimeModel>().Model;
        var entity = model.FindEntityType(typeof(Event))!;
        Assert.Equal(160, entity.FindProperty(nameof(Event.Name))!.GetMaxLength());
        Assert.Equal(10_000, entity.FindProperty(nameof(Event.Description))!.GetMaxLength());
        Assert.All(entity.GetIndexes().Where(index => index.Properties.Count == 3),
            index => Assert.Equal("\"DeletedAtUtc\" IS NULL", index.GetFilter()));
        var participant = model.FindEntityType(typeof(EventParticipant))!;
        Assert.Equal(new[] { "EventId", "UserId" },
            participant.FindPrimaryKey()!.Properties.Select(property => property.Name));
        var pendingIndex = Assert.Single(model.FindEntityType(typeof(EventInvitation))!.GetIndexes(),
            index => index.Properties.Select(property => property.Name).SequenceEqual(new[] { "EventId", "InviteeUserId" }));
        Assert.Equal("\"Status\" = 0", pendingIndex.GetFilter());
    }

    [Theory]
    [InlineData(typeof(Event), "CreatedByUser", "CreatedByUserId", typeof(User), DeleteBehavior.Restrict, true, false)]
    [InlineData(typeof(Event), "CoverMedia", "CoverMediaId", typeof(MediaAsset), DeleteBehavior.Restrict, false, false)]
    [InlineData(typeof(EventParticipant), "Event", "EventId", typeof(Event), DeleteBehavior.Cascade, true, false)]
    [InlineData(typeof(EventParticipant), "User", "UserId", typeof(User), DeleteBehavior.Restrict, true, false)]
    [InlineData(typeof(EventInvitation), "Event", "EventId", typeof(Event), DeleteBehavior.Cascade, true, false)]
    [InlineData(typeof(EventInvitation), "InviterUser", "InviterUserId", typeof(User), DeleteBehavior.Restrict, true, false)]
    [InlineData(typeof(EventInvitation), "InviteeUser", "InviteeUserId", typeof(User), DeleteBehavior.Restrict, true, false)]
    [InlineData(typeof(EventCoverMediaReference), "Event", "EventId", typeof(Event), DeleteBehavior.Cascade, true, true)]
    [InlineData(typeof(EventCoverMediaReference), "Media", "MediaId", typeof(MediaAsset), DeleteBehavior.Restrict, true, false)]
    public void Event_relationships_use_existing_keys_and_explicit_delete_behavior(
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
