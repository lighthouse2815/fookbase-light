using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Notifications.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Notifications.Entities;

[Table("Notifications")]
[Index(nameof(RecipientUserId), nameof(CreatedAtUtc), nameof(Id))]
[Index(nameof(RecipientUserId), nameof(IsRead), nameof(CreatedAtUtc))]
[Index(nameof(RecipientUserId), nameof(ActorUserId), nameof(Type), nameof(EntityType), nameof(EntityId))]
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

    [Key]
    public Guid Id { get; private set; }

    public Guid RecipientUserId { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public NotificationType Type { get; private set; }

    public NotificationEntityType? EntityType { get; private set; }

    public Guid? EntityId { get; private set; }

    public bool IsRead { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    [ForeignKey(nameof(RecipientUserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User RecipientUser { get; private set; } = null!;

    [ForeignKey(nameof(ActorUserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? ActorUser { get; private set; }

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
