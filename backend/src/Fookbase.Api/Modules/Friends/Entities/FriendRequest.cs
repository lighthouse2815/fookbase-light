using Fookbase.Api.Modules.Friends.Entities.Relationships;

namespace Fookbase.Api.Modules.Friends.Entities;

public enum FriendRequestStatus
{
    Pending,
    Accepted,
    Declined,
    Cancelled
}

public sealed class FriendRequest
{
    private FriendRequest()
    {
    }

    private FriendRequest(
        Guid id,
        Guid senderUserId,
        Guid receiverUserId,
        UserPair pair,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        SenderUserId = senderUserId;
        ReceiverUserId = receiverUserId;
        UserId1 = pair.UserId1;
        UserId2 = pair.UserId2;
        Status = FriendRequestStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid SenderUserId { get; private set; }

    public Guid ReceiverUserId { get; private set; }

    public Guid UserId1 { get; private set; }

    public Guid UserId2 { get; private set; }

    public FriendRequestStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? RespondedAtUtc { get; private set; }

    public static FriendRequest Create(
        Guid id,
        Guid senderUserId,
        Guid receiverUserId,
        DateTimeOffset createdAtUtc) =>
        new(id, senderUserId, receiverUserId, UserPair.Create(senderUserId, receiverUserId), createdAtUtc);

    public void Accept(Guid actorUserId, DateTimeOffset respondedAtUtc)
    {
        EnsureReceiver(actorUserId);
        EnsurePending();
        Status = FriendRequestStatus.Accepted;
        RespondedAtUtc = respondedAtUtc;
    }

    public void Decline(Guid actorUserId, DateTimeOffset respondedAtUtc)
    {
        EnsureReceiver(actorUserId);
        EnsurePending();
        Status = FriendRequestStatus.Declined;
        RespondedAtUtc = respondedAtUtc;
    }

    public void Cancel(Guid actorUserId, DateTimeOffset respondedAtUtc)
    {
        if (actorUserId != SenderUserId)
        {
            throw new UnauthorizedAccessException("Only the sender can cancel a friend request.");
        }

        EnsurePending();
        Status = FriendRequestStatus.Cancelled;
        RespondedAtUtc = respondedAtUtc;
    }

    public void CancelBecauseBlocked(DateTimeOffset respondedAtUtc)
    {
        EnsurePending();
        Status = FriendRequestStatus.Cancelled;
        RespondedAtUtc = respondedAtUtc;
    }

    private void EnsureReceiver(Guid actorUserId)
    {
        if (actorUserId != ReceiverUserId)
        {
            throw new UnauthorizedAccessException("Only the receiver can respond to a friend request.");
        }
    }

    private void EnsurePending()
    {
        if (Status != FriendRequestStatus.Pending)
        {
            throw new InvalidOperationException("The friend request is no longer pending.");
        }
    }
}
