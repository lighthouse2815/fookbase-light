using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Messages.Entities;
using Fookbase.Api.Modules.Stories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Fookbase.Messages.Api.IntegrationTests;

public sealed class MessageModelTests
{
    [Fact]
    public void Message_mapping_matches_the_migration_snapshot()
    {
        using var db = CreateDbContext();
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Message_mapping_preserves_keys_indexes_lengths_and_enum_storage()
    {
        using var db = CreateDbContext();
        var model = db.GetService<IDesignTimeModel>().Model;
        var conversation = model.FindEntityType(typeof(Conversation))!;
        Assert.Equal("Conversations", conversation.GetTableName());
        Assert.Equal(120, conversation.FindProperty(nameof(Conversation.Title))!.GetMaxLength());
        Assert.Equal("integer", conversation.FindProperty(nameof(Conversation.Type))!.GetColumnType());
        AssertIndex(conversation, ["UserId1", "UserId2"], unique: true);
        AssertIndex(conversation, ["LastMessageAtUtc", "Id"]);

        var participant = model.FindEntityType(typeof(ConversationParticipant))!;
        Assert.Equal(new[] { "ConversationId", "UserId" }, participant.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.Equal(80, participant.FindProperty(nameof(ConversationParticipant.Nickname))!.GetMaxLength());
        Assert.Equal("integer", participant.FindProperty(nameof(ConversationParticipant.Role))!.GetColumnType());
        Assert.Null(participant.FindProperty(nameof(ConversationParticipant.IsActive)));
        AssertIndex(participant, ["UserId", "ConversationId"]);
        AssertIndex(participant, ["ConversationId", "UserId"]);

        var cursor = model.FindEntityType(typeof(ConversationReadCursor))!;
        Assert.Equal(new[] { "ConversationId", "UserId" }, cursor.FindPrimaryKey()!.Properties.Select(p => p.Name));
        AssertIndex(cursor, ["UserId", "ConversationId"]);

        var message = model.FindEntityType(typeof(Message))!;
        Assert.Equal(5000, message.FindProperty(nameof(Message.Content))!.GetMaxLength());
        Assert.Equal("integer", message.FindProperty(nameof(Message.Type))!.GetColumnType());
        AssertIndex(message, ["ConversationId", "CreatedAtUtc", "Id"]);
        AssertIndex(message, ["ReplyToMessageId"]);
        AssertIndex(message, ["StoryId"]);

        var attachment = model.FindEntityType(typeof(MessageAttachment))!;
        Assert.Equal(new[] { "MessageId", "MediaId" }, attachment.FindPrimaryKey()!.Properties.Select(p => p.Name));
        AssertIndex(attachment, ["MediaId"]);
        AssertIndex(attachment, ["MessageId", "SortOrder"], unique: true);

        var reaction = model.FindEntityType(typeof(MessageReaction))!;
        Assert.Equal(new[] { "MessageId", "UserId" }, reaction.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.Equal("integer", reaction.FindProperty(nameof(MessageReaction.Type))!.GetColumnType());
        AssertIndex(reaction, ["MessageId", "Type"]);

        var notification = model.FindEntityType(typeof(MessageNotification))!;
        AssertIndex(notification, ["RecipientUserId", "MessageId"], unique: true);
        AssertIndex(notification, ["RecipientUserId", "ReadAtUtc", "CreatedAtUtc"]);
    }

    [Theory]
    [InlineData(typeof(Conversation), "User1", "UserId1", typeof(User), DeleteBehavior.Restrict, false, null)]
    [InlineData(typeof(Conversation), "User2", "UserId2", typeof(User), DeleteBehavior.Restrict, false, null)]
    [InlineData(typeof(Conversation), "PhotoMedia", "PhotoMediaId", typeof(MediaAsset), DeleteBehavior.Restrict, false, null)]
    [InlineData(typeof(ConversationParticipant), "Conversation", "ConversationId", typeof(Conversation), DeleteBehavior.Cascade, true, "Participants")]
    [InlineData(typeof(ConversationParticipant), "User", "UserId", typeof(User), DeleteBehavior.Restrict, true, null)]
    [InlineData(typeof(ConversationParticipant), "LastReadMessage", "LastReadMessageId", typeof(Message), DeleteBehavior.Restrict, false, null)]
    [InlineData(typeof(ConversationParticipant), "LastDeliveredMessage", "LastDeliveredMessageId", typeof(Message), DeleteBehavior.Restrict, false, null)]
    [InlineData(typeof(ConversationReadCursor), "Conversation", "ConversationId", typeof(Conversation), DeleteBehavior.Cascade, true, "ReadCursors")]
    [InlineData(typeof(ConversationReadCursor), "User", "UserId", typeof(User), DeleteBehavior.Restrict, true, null)]
    [InlineData(typeof(ConversationReadCursor), "LastReadMessage", "LastReadMessageId", typeof(Message), DeleteBehavior.Restrict, false, null)]
    [InlineData(typeof(Message), "Conversation", "ConversationId", typeof(Conversation), DeleteBehavior.Cascade, true, "Messages")]
    [InlineData(typeof(Message), "SenderUser", "SenderUserId", typeof(User), DeleteBehavior.Restrict, true, null)]
    [InlineData(typeof(Message), "ReplyToMessage", "ReplyToMessageId", typeof(Message), DeleteBehavior.Restrict, false, null)]
    [InlineData(typeof(Message), "Story", "StoryId", typeof(Story), DeleteBehavior.Restrict, false, null)]
    [InlineData(typeof(MessageAttachment), "Message", "MessageId", typeof(Message), DeleteBehavior.Cascade, true, "Attachments")]
    [InlineData(typeof(MessageAttachment), "Media", "MediaId", typeof(MediaAsset), DeleteBehavior.Restrict, true, null)]
    [InlineData(typeof(MessageReaction), "Message", "MessageId", typeof(Message), DeleteBehavior.Cascade, true, "Reactions")]
    [InlineData(typeof(MessageReaction), "User", "UserId", typeof(User), DeleteBehavior.Restrict, true, null)]
    [InlineData(typeof(MessageNotification), "RecipientUser", "RecipientUserId", typeof(User), DeleteBehavior.Restrict, true, null)]
    [InlineData(typeof(MessageNotification), "Conversation", "ConversationId", typeof(Conversation), DeleteBehavior.Cascade, true, "Notifications")]
    [InlineData(typeof(MessageNotification), "Message", "MessageId", typeof(Message), DeleteBehavior.Cascade, true, "Notifications")]
    public void Message_relationships_use_explicit_keys_without_shadow_columns(
        Type entityType, string navigation, string property, Type principal, DeleteBehavior behavior, bool required, string? inverse)
    {
        using var db = CreateDbContext();
        var entity = db.Model.FindEntityType(entityType)!;
        var relationship = entity.FindNavigation(navigation);
        Assert.NotNull(relationship);
        var foreignKey = relationship.ForeignKey;
        Assert.Equal(principal, foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(property, Assert.Single(foreignKey.Properties).Name);
        Assert.Equal(required, foreignKey.IsRequired);
        Assert.Equal(behavior, foreignKey.DeleteBehavior);
        Assert.False(foreignKey.IsUnique);
        Assert.Equal(inverse, foreignKey.PrincipalToDependent?.Name);
        Assert.DoesNotContain(entity.GetProperties(), item => item.IsShadowProperty());
    }

    private static void AssertIndex(IEntityType entity, string[] properties, bool unique = false)
    {
        var index = Assert.Single(entity.GetIndexes(), item => item.Properties.Select(p => p.Name).SequenceEqual(properties));
        Assert.Equal(unique, index.IsUnique);
        Assert.Null(index.GetFilter());
    }

    private static FookbaseDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<FookbaseDbContext>()
            .UseNpgsql("Host=localhost;Database=fookbase_model;Username=fookbase;Password=fookbase")
            .Options);
}
