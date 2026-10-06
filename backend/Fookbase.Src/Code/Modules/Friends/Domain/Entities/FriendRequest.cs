using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Friends.Domain.Enums;
using Fookbase.Api.Modules.Friends.Domain.ValueObjects;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Persistence.Annotations;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Friends.Entities;

[Index(nameof(User1Id), nameof(User2Id), IsUnique = true, Name = "UX_FriendRequests_PendingPair")]
[IndexFilter("\"Status\" = 0", nameof(User1Id), nameof(User2Id))]
[Index(nameof(ReceiverUserId), nameof(Status), nameof(CreatedAtUtc))]
[Index(nameof(SenderUserId), nameof(Status), nameof(CreatedAtUtc))]
[CheckConstraint("CK_FriendRequests_DifferentUsers", "\"SenderUserId\" <> \"ReceiverUserId\"")]
[CheckConstraint("CK_FriendRequests_CanonicalPair", "\"UserId1\" < \"UserId2\"")]
public sealed class FriendRequest
{
    private FriendRequest() { }

    public FriendRequest(
        Guid senderUserId,
        Guid receiverUserId,
        DateTimeOffset createdAtUtc)
    {
        var pair = UserPair.Create(senderUserId, receiverUserId);
        Id = Guid.NewGuid();
        SenderUserId = senderUserId;
        ReceiverUserId = receiverUserId;
        User1Id = pair.UserId1;
        User2Id = pair.UserId2;
        Status = FriendRequestStatus.PENDING;
        CreatedAtUtc = createdAtUtc;
    }

    [Key]
    public Guid Id { get; private set; }

    public Guid SenderUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User SenderUser { get; private set; } = null!;

    public Guid ReceiverUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User ReceiverUser { get; private set; } = null!;

    [Column("UserId1")]
    public Guid User1Id { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User User1 { get; private set; } = null!;

    [Column("UserId2")]
    public Guid User2Id { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User User2 { get; private set; } = null!;

    public FriendRequestStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? RespondedAtUtc { get; private set; }

    [InverseProperty(nameof(FriendNotification.FriendRequest))]
    public ICollection<FriendNotification> Notifications { get; } = [];

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
