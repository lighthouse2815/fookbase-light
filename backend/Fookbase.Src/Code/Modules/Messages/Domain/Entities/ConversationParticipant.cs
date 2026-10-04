using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Messages.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Messages.Entities;

[Table("ConversationParticipants")]
[PrimaryKey(nameof(ConversationId), nameof(UserId))]
[Index(nameof(UserId), nameof(ConversationId))]
[Index(nameof(ConversationId), nameof(UserId))]
public sealed class ConversationParticipant
{
    public const int MaximumNicknameLength = 80;

    private ConversationParticipant()
    {
    }

    public ConversationParticipant(
        Guid conversationId,
        Guid userId,
        ConversationParticipantRole role,
        DateTimeOffset joinedAtUtc)
    {
        ConversationId = conversationId;
        UserId = userId;
        Role = role;
        JoinedAtUtc = joinedAtUtc;
    }

    public Guid ConversationId { get; private set; }
    public Guid UserId { get; private set; }
    public ConversationParticipantRole Role { get; private set; }
    public DateTimeOffset JoinedAtUtc { get; private set; }
    public DateTimeOffset? LeftAtUtc { get; private set; }
    public Guid? LastReadMessageId { get; private set; }
    public DateTimeOffset? LastReadMessageCreatedAtUtc { get; private set; }
    public DateTimeOffset? LastReadAtUtc { get; private set; }
    public Guid? LastDeliveredMessageId { get; private set; }
    public DateTimeOffset? MutedUntilUtc { get; private set; }
    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    [MaxLength(MaximumNicknameLength)]
    public string? Nickname { get; private set; }

    [NotMapped]
    public bool IsActive => LeftAtUtc is null;

    [ForeignKey(nameof(ConversationId))]
    [InverseProperty(nameof(Conversation.Participants))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Conversation Conversation { get; private set; } = null!;

    [ForeignKey(nameof(UserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User User { get; private set; } = null!;

    [ForeignKey(nameof(LastReadMessageId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Message? LastReadMessage { get; private set; }

    [ForeignKey(nameof(LastDeliveredMessageId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Message? LastDeliveredMessage { get; private set; }

    public void ChangeRole(ConversationParticipantRole role) => Role = role;

    public void Leave(DateTimeOffset leftAtUtc) => LeftAtUtc ??= leftAtUtc;

    public void Rejoin(ConversationParticipantRole role, DateTimeOffset joinedAtUtc)
    {
        Role = role;
        JoinedAtUtc = joinedAtUtc;
        LeftAtUtc = null;
    }

    public bool AdvanceReadCursor(Guid messageId, DateTimeOffset messageCreatedAtUtc, DateTimeOffset readAtUtc)
    {
        if (LastReadMessageCreatedAtUtc is not null)
        {
            var chronology = messageCreatedAtUtc.CompareTo(LastReadMessageCreatedAtUtc.Value);
            if (chronology < 0 ||
                chronology == 0 && LastReadMessageId is not null && messageId.CompareTo(LastReadMessageId.Value) <= 0)
            {
                return false;
            }
        }

        LastReadMessageId = messageId;
        LastReadMessageCreatedAtUtc = messageCreatedAtUtc;
        LastReadAtUtc = readAtUtc;
        return true;
    }

    public void MarkDelivered(Guid messageId) => LastDeliveredMessageId = messageId;

    public void SetMutedUntil(DateTimeOffset? mutedUntilUtc) => MutedUntilUtc = mutedUntilUtc;

    public void SetArchived(bool archived, DateTimeOffset now) => ArchivedAtUtc = archived ? now : null;

    public void SetNickname(string? nickname)
    {
        var normalized = string.IsNullOrWhiteSpace(nickname) ? null : nickname.Trim();
        if (normalized?.Length > MaximumNicknameLength)
        {
            throw new ArgumentException("A participant nickname cannot exceed 80 characters.");
        }

        Nickname = normalized;
    }
}
