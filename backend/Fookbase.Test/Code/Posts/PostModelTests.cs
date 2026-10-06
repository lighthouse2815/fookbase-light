using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class PostModelTests
{
    [Fact]
    public void Post_mapping_matches_the_migration_snapshot()
    {
        using var dbContext = CreateDbContext();

        Assert.False(dbContext.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData(typeof(Comment), nameof(Comment.Post), nameof(Comment.PostId), typeof(Post), DeleteBehavior.Cascade)]
    [InlineData(typeof(Comment), nameof(Comment.ParentComment), nameof(Comment.ParentCommentId), typeof(Comment), DeleteBehavior.Restrict)]
    [InlineData(typeof(CommentReaction), nameof(CommentReaction.Comment), nameof(CommentReaction.CommentId), typeof(Comment), DeleteBehavior.Cascade)]
    [InlineData(typeof(ContentMention), nameof(ContentMention.MentionedUser), nameof(ContentMention.MentionedUserId), typeof(User), DeleteBehavior.Cascade)]
    [InlineData(typeof(PostHashtag), nameof(PostHashtag.Post), nameof(PostHashtag.PostId), typeof(Post), DeleteBehavior.Cascade)]
    [InlineData(typeof(PostHashtag), nameof(PostHashtag.Hashtag), nameof(PostHashtag.HashtagId), typeof(Hashtag), DeleteBehavior.Cascade)]
    [InlineData(typeof(PostMedia), nameof(PostMedia.Post), nameof(PostMedia.PostId), typeof(Post), DeleteBehavior.Cascade)]
    [InlineData(typeof(PostReaction), nameof(PostReaction.Post), nameof(PostReaction.PostId), typeof(Post), DeleteBehavior.Cascade)]
    [InlineData(typeof(PostSave), nameof(PostSave.Post), nameof(PostSave.PostId), typeof(Post), DeleteBehavior.Cascade)]
    [InlineData(typeof(PostShare), nameof(PostShare.OriginalPost), nameof(PostShare.OriginalPostId), typeof(Post), DeleteBehavior.Cascade)]
    [InlineData(typeof(Post), "AuthorUser", nameof(Post.AuthorUserId), typeof(User), DeleteBehavior.Restrict)]
    [InlineData(typeof(Comment), "AuthorUser", nameof(Comment.AuthorUserId), typeof(User), DeleteBehavior.Restrict)]
    [InlineData(typeof(CommentReaction), "User", nameof(CommentReaction.UserId), typeof(User), DeleteBehavior.Restrict)]
    [InlineData(typeof(ContentReport), "ReporterUser", nameof(ContentReport.ReporterUserId), typeof(User), DeleteBehavior.Restrict)]
    [InlineData(typeof(PostMedia), "Media", nameof(PostMedia.MediaId), typeof(MediaAsset), DeleteBehavior.Restrict)]
    [InlineData(typeof(PostReaction), "User", nameof(PostReaction.UserId), typeof(User), DeleteBehavior.Restrict)]
    [InlineData(typeof(PostSave), "User", nameof(PostSave.UserId), typeof(User), DeleteBehavior.Restrict)]
    [InlineData(typeof(PostShare), "SharingUser", nameof(PostShare.SharingUserId), typeof(User), DeleteBehavior.Restrict)]
    public void Domain_navigation_uses_the_existing_foreign_key(
        Type entityType,
        string navigationName,
        string propertyName,
        Type principalType,
        DeleteBehavior deleteBehavior)
    {
        using var dbContext = CreateDbContext();
        var entity = dbContext.Model.FindEntityType(entityType)!;
        var navigation = entity.FindNavigation(navigationName);
        Assert.NotNull(navigation);
        var foreignKey = navigation.ForeignKey;

        Assert.Equal(principalType, foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(propertyName, Assert.Single(foreignKey.Properties).Name);
        Assert.Equal(deleteBehavior, foreignKey.DeleteBehavior);
        Assert.Equal(propertyName != nameof(Comment.ParentCommentId), foreignKey.IsRequired);
        Assert.False(foreignKey.IsUnique);
        Assert.DoesNotContain(entity.GetProperties(), property => property.IsShadowProperty());
    }

    [Theory]
    [InlineData(typeof(Comment), nameof(Comment.Post), "Comments")]
    [InlineData(typeof(Comment), nameof(Comment.ParentComment), "Replies")]
    [InlineData(typeof(CommentReaction), nameof(CommentReaction.Comment), "Reactions")]
    [InlineData(typeof(PostHashtag), nameof(PostHashtag.Post), "Hashtags")]
    [InlineData(typeof(PostHashtag), nameof(PostHashtag.Hashtag), "Posts")]
    [InlineData(typeof(PostMedia), nameof(PostMedia.Post), "MediaItems")]
    [InlineData(typeof(PostReaction), nameof(PostReaction.Post), "Reactions")]
    [InlineData(typeof(PostSave), nameof(PostSave.Post), "Saves")]
    [InlineData(typeof(PostShare), nameof(PostShare.OriginalPost), "Shares")]
    public void Post_relationships_map_the_inverse_collection_without_extra_foreign_keys(
        Type entityType,
        string navigationName,
        string collectionName)
    {
        using var dbContext = CreateDbContext();
        var navigation = dbContext.Model.FindEntityType(entityType)!.FindNavigation(navigationName)!;
        var inverse = navigation.Inverse;

        Assert.NotNull(inverse);
        Assert.Equal(collectionName, inverse.Name);
        Assert.True(inverse.IsCollection);
        Assert.Same(navigation.ForeignKey, inverse.ForeignKey);
    }

    private static FookbaseDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<FookbaseDbContext>()
            .UseNpgsql("Host=localhost;Database=fookbase_model;Username=fookbase;Password=fookbase")
            .Options);
}
