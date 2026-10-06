using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Friends.Domain.Enums;
using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Friends.Entities;

[Index(nameof(RecipientUserId), nameof(ReadAtUtc), nameof(CreatedAtUtc))]
public sealed class FriendNotification
{
    private FriendNotification() { }

    public FriendNotification(
        Guid recipientUserId,
        Guid actorUserId,
        Guid friendRequestId,
        FriendNotificationType type,
        DateTimeOffset createdAtUtc)
    {
        Id = Guid.NewGuid();
        RecipientUserId = recipientUserId;
        ActorUserId = actorUserId;
        FriendRequestId = friendRequestId;
        Type = type;
        CreatedAtUtc = createdAtUtc;
    }

    [Key]
    public Guid Id { get; private set; }

    public Guid RecipientUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User RecipientUser { get; private set; } = null!;

    public Guid ActorUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User ActorUser { get; private set; } = null!;

    public Guid FriendRequestId { get; private set; }

    [InverseProperty(nameof(FriendRequest.Notifications))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public FriendRequest FriendRequest { get; private set; } = null!;

    public FriendNotificationType Type { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    public void MarkRead(DateTimeOffset readAtUtc) => ReadAtUtc ??= readAtUtc;
}
