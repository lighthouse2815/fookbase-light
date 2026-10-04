using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class PostModelTests
{
    [Fact]
    public void Domain_mapping_preserves_the_existing_database_schema()
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
    public void Domain_navigation_uses_the_existing_foreign_key(
        Type entityType,
        string navigationName,
        string propertyName,
        Type principalType,
        DeleteBehavior deleteBehavior)
    {
        using var dbContext = CreateDbContext();
        var entity = dbContext.Model.FindEntityType(entityType)!;
        var foreignKey = entity.FindNavigation(navigationName)!.ForeignKey;

        Assert.Equal(principalType, foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(propertyName, Assert.Single(foreignKey.Properties).Name);
        Assert.Equal(deleteBehavior, foreignKey.DeleteBehavior);
        Assert.Equal(propertyName != nameof(Comment.ParentCommentId), foreignKey.IsRequired);
        Assert.False(foreignKey.IsUnique);
        Assert.DoesNotContain(entity.GetProperties(), property => property.IsShadowProperty());
    }

    private static FookbaseDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<FookbaseDbContext>()
            .UseNpgsql("Host=localhost;Database=fookbase_model;Username=fookbase;Password=fookbase")
            .Options);
}
