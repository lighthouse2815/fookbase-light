namespace Fookbase.Api.Modules.Friends.Entities;

public enum FriendNotificationType
{
    FriendRequestReceived,
    FriendRequestAccepted
}

public sealed class FriendNotification
{
    private FriendNotification()
    {
    }

    private FriendNotification(
        Guid id,
        Guid recipientUserId,
        Guid actorUserId,
        Guid friendRequestId,
        FriendNotificationType type,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        RecipientUserId = recipientUserId;
        ActorUserId = actorUserId;
        FriendRequestId = friendRequestId;
        Type = type;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid RecipientUserId { get; private set; }

    public Guid ActorUserId { get; private set; }

    public Guid FriendRequestId { get; private set; }

    public FriendNotificationType Type { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    public static FriendNotification Create(
        Guid id,
        Guid recipientUserId,
        Guid actorUserId,
        Guid friendRequestId,
        FriendNotificationType type,
        DateTimeOffset createdAtUtc) =>
        new(id, recipientUserId, actorUserId, friendRequestId, type, createdAtUtc);

    public void MarkRead(DateTimeOffset readAtUtc) => ReadAtUtc ??= readAtUtc;
}
