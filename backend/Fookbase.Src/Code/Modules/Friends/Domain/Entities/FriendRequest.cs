using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Fookbase.Api.Persistence.Annotations;
using Fookbase.Api.Modules.Friends.Domain.Enums;
using Fookbase.Api.Modules.Friends.Domain.ValueObjects;

namespace Fookbase.Api.Modules.Friends.Entities;

[Index(nameof(UserId1), nameof(UserId2), IsUnique = true, Name = "UX_FriendRequests_PendingPair")]
[IndexFilter("\"Status\" = 0", nameof(UserId1), nameof(UserId2))]
[Index(nameof(ReceiverUserId), nameof(Status), nameof(CreatedAtUtc))]
[Index(nameof(SenderUserId), nameof(Status), nameof(CreatedAtUtc))]
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
        Status = FriendRequestStatus.PENDING;
        CreatedAtUtc = createdAtUtc;
    }

    [Key]
    public Guid Id { get; private set; }

    public Guid SenderUserId { get; private set; }

    public Guid ReceiverUserId { get; private set; }

    public Guid UserId1 { get; private set; }

    public Guid UserId2 { get; private set; }

    public FriendRequestStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? RespondedAtUtc { get; private set; }

    [ForeignKey(nameof(SenderUserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User SenderUser { get; private set; } = null!;

    [ForeignKey(nameof(ReceiverUserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User ReceiverUser { get; private set; } = null!;

    [ForeignKey(nameof(UserId1))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User User1 { get; private set; } = null!;

    [ForeignKey(nameof(UserId2))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User User2 { get; private set; } = null!;

    [InverseProperty(nameof(FriendNotification.FriendRequest))]
    public ICollection<FriendNotification> Notifications { get; } = [];

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
        Status = FriendRequestStatus.ACCEPTED;
        RespondedAtUtc = respondedAtUtc;
    }

    public void Decline(Guid actorUserId, DateTimeOffset respondedAtUtc)
    {
        EnsureReceiver(actorUserId);
        EnsurePending();
        Status = FriendRequestStatus.DECLINED;
        RespondedAtUtc = respondedAtUtc;
    }

    public void Cancel(Guid actorUserId, DateTimeOffset respondedAtUtc)
    {
        if (actorUserId != SenderUserId)
        {
            throw new UnauthorizedAccessException("Only the sender can cancel a friend request.");
        }

        EnsurePending();
        Status = FriendRequestStatus.CANCELLED;
        RespondedAtUtc = respondedAtUtc;
    }

    public void CancelBecauseBlocked(DateTimeOffset respondedAtUtc)
    {
        EnsurePending();
        Status = FriendRequestStatus.CANCELLED;
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
        if (Status != FriendRequestStatus.PENDING)
        {
            throw new InvalidOperationException("The friend request is no longer pending.");
        }
    }
}
