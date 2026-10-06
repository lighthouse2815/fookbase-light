using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Fookbase.Api.Modules.Friends.Domain.Enums;

namespace Fookbase.Api.Modules.Friends.Entities;

[Index(nameof(RecipientUserId), nameof(ReadAtUtc), nameof(CreatedAtUtc))]
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

    [Key]
    public Guid Id { get; private set; }

    public Guid RecipientUserId { get; private set; }

    public Guid ActorUserId { get; private set; }

    public Guid FriendRequestId { get; private set; }

    public FriendNotificationType Type { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    [ForeignKey(nameof(RecipientUserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User RecipientUser { get; private set; } = null!;

    [ForeignKey(nameof(ActorUserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User ActorUser { get; private set; } = null!;

    [ForeignKey(nameof(FriendRequestId))]
    [InverseProperty(nameof(FriendRequest.Notifications))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public FriendRequest FriendRequest { get; private set; } = null!;

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
