using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Groups.Domain.Enums;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Persistence.Annotations;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Groups.Entities;

[Index(nameof(GroupId), nameof(RequesterUserId), IsUnique = true)]
[IndexFilter("\"Status\" = 0", nameof(GroupId), nameof(RequesterUserId))]
[Index(nameof(GroupId), nameof(Status), nameof(CreatedAtUtc))]
public sealed class GroupJoinRequest
{
    private GroupJoinRequest() { }

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

    [InverseProperty(nameof(Group.JoinRequests))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Group Group { get; private set; } = null!;

    public Guid RequesterUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User RequesterUser { get; private set; } = null!;

    public Guid? RespondedByUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? RespondedByUser { get; private set; }

    public GroupJoinRequestStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? RespondedAtUtc { get; private set; }

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
