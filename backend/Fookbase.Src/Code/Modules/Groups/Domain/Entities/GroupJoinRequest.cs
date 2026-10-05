using Fookbase.Api.Modules.Groups.Domain.Enums;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Persistence.Annotations;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Groups.Entities;

[Table("GroupJoinRequests")]
[Index(nameof(GroupId), nameof(RequesterUserId), IsUnique = true)]
[IndexFilter("\"Status\" = 0", nameof(GroupId), nameof(RequesterUserId))]
[Index(nameof(GroupId), nameof(Status), nameof(CreatedAtUtc))]
public sealed class GroupJoinRequest
{
    private GroupJoinRequest()
    {
    }

    public GroupJoinRequest(Guid id, Guid groupId, Guid requesterUserId, DateTimeOffset createdAtUtc)
    {
        Id = id;
        GroupId = groupId;
        RequesterUserId = requesterUserId;
        Status = GroupJoinRequestStatus.PENDING;
        CreatedAtUtc = createdAtUtc;
    }

    [Key]
    public Guid Id { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid RequesterUserId { get; private set; }
    public GroupJoinRequestStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? RespondedAtUtc { get; private set; }
    public Guid? RespondedByUserId { get; private set; }

    [ForeignKey(nameof(GroupId))]
    [InverseProperty(nameof(Group.JoinRequests))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Group Group { get; private set; } = null!;

    [ForeignKey(nameof(RequesterUserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User RequesterUser { get; private set; } = null!;

    [ForeignKey(nameof(RespondedByUserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? RespondedByUser { get; private set; }

    public void Approve(Guid actorUserId, DateTimeOffset respondedAtUtc)
    {
        EnsurePending();
        Status = GroupJoinRequestStatus.APPROVED;
        RespondedByUserId = actorUserId;
        RespondedAtUtc = respondedAtUtc;
    }

    public void Decline(Guid actorUserId, DateTimeOffset respondedAtUtc)
    {
        EnsurePending();
        Status = GroupJoinRequestStatus.DECLINED;
        RespondedByUserId = actorUserId;
        RespondedAtUtc = respondedAtUtc;
    }

    private void EnsurePending()
    {
        if (Status != GroupJoinRequestStatus.PENDING)
        {
            throw new InvalidOperationException("Only pending group join requests can be changed.");
        }
    }
}
