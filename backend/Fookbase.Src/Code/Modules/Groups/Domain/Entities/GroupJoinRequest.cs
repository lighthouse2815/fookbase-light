using Fookbase.Api.Modules.Groups.Domain.Enums;

namespace Fookbase.Api.Modules.Groups.Entities;

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

    public Guid Id { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid RequesterUserId { get; private set; }
    public GroupJoinRequestStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? RespondedAtUtc { get; private set; }
    public Guid? RespondedByUserId { get; private set; }

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
