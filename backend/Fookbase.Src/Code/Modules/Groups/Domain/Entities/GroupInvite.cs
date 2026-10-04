using Fookbase.Api.Modules.Groups.Domain.Enums;
using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Groups.Entities;

[Table("GroupInvites")]
[Index(nameof(GroupId), nameof(InviteeUserId), IsUnique = true)]
[Index(nameof(InviteeUserId), nameof(Status), nameof(CreatedAtUtc))]
public sealed class GroupInvite
{
    private GroupInvite()
    {
    }

    public GroupInvite(
        Guid id,
        Guid groupId,
        Guid inviterUserId,
        Guid inviteeUserId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        GroupId = groupId;
        InviterUserId = inviterUserId;
        InviteeUserId = inviteeUserId;
        Status = GroupInviteStatus.PENDING;
        CreatedAtUtc = createdAtUtc;
    }

    [Key]
    public Guid Id { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid InviterUserId { get; private set; }
    public Guid InviteeUserId { get; private set; }
    public GroupInviteStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? RespondedAtUtc { get; private set; }

    [ForeignKey(nameof(GroupId))]
    [InverseProperty(nameof(Group.Invites))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Group Group { get; private set; } = null!;

    [ForeignKey(nameof(InviterUserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User InviterUser { get; private set; } = null!;

    [ForeignKey(nameof(InviteeUserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User InviteeUser { get; private set; } = null!;

    public void Accept(DateTimeOffset respondedAtUtc)
    {
        EnsurePending();
        Status = GroupInviteStatus.ACCEPTED;
        RespondedAtUtc = respondedAtUtc;
    }

    public void Decline(DateTimeOffset respondedAtUtc)
    {
        EnsurePending();
        Status = GroupInviteStatus.DECLINED;
        RespondedAtUtc = respondedAtUtc;
    }

    private void EnsurePending()
    {
        if (Status != GroupInviteStatus.PENDING)
        {
            throw new InvalidOperationException("Only pending group invites can be changed.");
        }
    }
}
