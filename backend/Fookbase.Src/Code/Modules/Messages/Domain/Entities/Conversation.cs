using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Messages.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Messages.Entities;

[Table("Conversations")]
[Index(nameof(UserId1), nameof(UserId2), IsUnique = true)]
[Index(nameof(LastMessageAtUtc), nameof(Id))]
public sealed class Conversation
{
    public const int MaximumTitleLength = 120;

    private Conversation() { }

    public Conversation(
        Guid id,
        Guid userId1,
        Guid userId2,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Type = ConversationType.DIRECT;
        if (userId1 == userId2)
        {
            throw new ArgumentException("A conversation requires two different users.");
        }

        (UserId1, UserId2) = userId1.CompareTo(userId2) < 0
            ? (userId1, userId2)
            : (userId2, userId1);
        CreatedAtUtc = createdAtUtc;
        LastMessageAtUtc = createdAtUtc;
    }

    public Conversation(
        Guid id,
        string title,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Type = ConversationType.GROUP;
        Title = NormalizeTitle(title);
        CreatedAtUtc = createdAtUtc;
        LastMessageAtUtc = createdAtUtc;
    }

    [Key]
    public Guid Id { get; private set; }

    // Direct conversations retain this canonical pair to preserve existing IDs and
    // enforce a database-safe uniqueness constraint. Group conversations use rows
    // in ConversationParticipants instead.
    public Guid? UserId1 { get; private set; }

    public Guid? UserId2 { get; private set; }

    public ConversationType Type { get; private set; }

    [MaxLength(MaximumTitleLength)]
    public string? Title { get; private set; }

    public Guid? PhotoMediaId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset LastMessageAtUtc { get; private set; }

    [ForeignKey(nameof(UserId1))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? User1 { get; private set; }

    [ForeignKey(nameof(UserId2))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? User2 { get; private set; }

    [ForeignKey(nameof(PhotoMediaId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset? PhotoMedia { get; private set; }

    [InverseProperty(nameof(ConversationParticipant.Conversation))]
    public ICollection<ConversationParticipant> Participants { get; private set; } = new List<ConversationParticipant>();

    [InverseProperty(nameof(ConversationReadCursor.Conversation))]
    public ICollection<ConversationReadCursor> ReadCursors { get; private set; } = new List<ConversationReadCursor>();

    [InverseProperty(nameof(Message.Conversation))]
    public ICollection<Message> Messages { get; private set; } = new List<Message>();

    [InverseProperty(nameof(MessageNotification.Conversation))]
    public ICollection<MessageNotification> Notifications { get; private set; } = new List<MessageNotification>();

    public bool Contains(Guid userId) => UserId1 == userId || UserId2 == userId;

    public Guid OtherUserId(Guid userId) =>
        UserId1 == userId
            ? UserId2!.Value
            : UserId2 == userId
                ? UserId1!.Value
                : throw new UnauthorizedAccessException("The user is not part of this conversation.");

    public void RecordMessage(DateTimeOffset createdAtUtc) => LastMessageAtUtc = createdAtUtc;

    public void UpdateGroup(string title, Guid? photoMediaId)
    {
        if (Type != ConversationType.GROUP)
        {
            throw new InvalidOperationException("Only group conversations can be updated.");
        }

        Title = NormalizeTitle(title);
        PhotoMediaId = photoMediaId;
    }

    private static string NormalizeTitle(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > MaximumTitleLength)
        {
            throw new ArgumentException("Group conversation title must contain between 1 and 120 characters.");
        }

        return normalized;
    }
}
