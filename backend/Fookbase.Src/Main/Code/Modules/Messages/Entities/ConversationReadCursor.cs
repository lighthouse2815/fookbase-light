namespace Fookbase.Api.Modules.Messages.Entities;

public sealed class ConversationReadCursor
{
    private ConversationReadCursor()
    {
    }

    private ConversationReadCursor(Guid conversationId, Guid userId)
    {
        ConversationId = conversationId;
        UserId = userId;
    }

    public Guid ConversationId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid? LastReadMessageId { get; private set; }

    public DateTimeOffset? LastReadMessageCreatedAtUtc { get; private set; }

    public DateTimeOffset? LastReadAtUtc { get; private set; }

    public static ConversationReadCursor Create(Guid conversationId, Guid userId) =>
        new(conversationId, userId);

    public bool AdvanceTo(Guid messageId, DateTimeOffset messageCreatedAtUtc, DateTimeOffset readAtUtc)
    {
        if (LastReadMessageCreatedAtUtc is not null)
        {
            var chronology = messageCreatedAtUtc.CompareTo(LastReadMessageCreatedAtUtc.Value);
            if (chronology < 0 ||
                chronology == 0 &&
                LastReadMessageId is not null &&
                messageId.CompareTo(LastReadMessageId.Value) <= 0)
            {
                return false;
            }
        }

        LastReadMessageId = messageId;
        LastReadMessageCreatedAtUtc = messageCreatedAtUtc;
        LastReadAtUtc = readAtUtc;
        return true;
    }
}
