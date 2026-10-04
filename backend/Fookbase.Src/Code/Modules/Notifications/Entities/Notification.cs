namespace Fookbase.Api.Modules.Notifications.Entities;

public enum NotificationType
{
    FRIEND_REQUEST_RECEIVED,
    FRIEND_REQUEST_ACCEPTED,
    POST_REACTION,
    POST_COMMENT,
    COMMENT_REACTION,
    POST_SHARED,
    POST_MENTION,
    COMMENT_MENTION,
    GROUP_INVITE,
    GROUP_JOIN_APPROVED,
    STORY_REACTION,
    PAGE_ROLE_INVITE,
    USER_FOLLOWED,
    EVENT_INVITE,
    EVENT_UPDATED,
    EVENT_CANCELLED
    ,ACCOUNT_WARNING
}

public enum NotificationEntityType
{
    FRIEND_REQUEST,
    POST,
    COMMENT,
    GROUP,
    GROUP_JOIN_REQUEST,
    GROUP_INVITE,
    STORY,
    PAGE,
    PAGE_ROLE_INVITATION,
    USER_FOLLOW,
    EVENT
}

public sealed class Notification
{
    private Notification()
    {
    }

    private Notification(
        Guid id,
        Guid recipientUserId,
        Guid? actorUserId,
        NotificationType type,
        NotificationEntityType? entityType,
        Guid? entityId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        RecipientUserId = recipientUserId;
        ActorUserId = actorUserId;
        Type = type;
        EntityType = entityType;
        EntityId = entityId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid RecipientUserId { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public NotificationType Type { get; private set; }

    public NotificationEntityType? EntityType { get; private set; }

    public Guid? EntityId { get; private set; }

    public bool IsRead { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    public static Notification Create(
        Guid id,
        Guid recipientUserId,
        Guid? actorUserId,
        NotificationType type,
        NotificationEntityType? entityType,
        Guid? entityId,
        DateTimeOffset createdAtUtc) =>
        new(id, recipientUserId, actorUserId, type, entityType, entityId, createdAtUtc);

    public void Refresh(DateTimeOffset createdAtUtc)
    {
        CreatedAtUtc = createdAtUtc;
        IsRead = false;
        ReadAtUtc = null;
    }

    public void MarkRead(DateTimeOffset readAtUtc)
    {
        if (IsRead)
        {
            return;
        }

        IsRead = true;
        ReadAtUtc = readAtUtc;
    }
}
