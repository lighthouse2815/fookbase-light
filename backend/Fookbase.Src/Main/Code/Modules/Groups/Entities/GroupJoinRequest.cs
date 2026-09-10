namespace Fookbase.Api.Modules.Groups.Entities;

public enum GroupJoinRequestStatus
{
    Pending,
    Approved,
    Declined
}

public sealed class GroupJoinRequest
{
    private GroupJoinRequest()
    {
    }

    private GroupJoinRequest(Guid id, Guid groupId, Guid requesterUserId, DateTimeOffset createdAtUtc)
    {
        Id = id;
        GroupId = groupId;
        RequesterUserId = requesterUserId;
        Status = GroupJoinRequestStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid RequesterUserId { get; private set; }
    public GroupJoinRequestStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? RespondedAtUtc { get; private set; }
    public Guid? RespondedByUserId { get; private set; }

    public static GroupJoinRequest Create(
        Guid id,
        Guid groupId,
        Guid requesterUserId,
        DateTimeOffset createdAtUtc) =>
        new(id, groupId, requesterUserId, createdAtUtc);

    public void Approve(Guid actorUserId, DateTimeOffset respondedAtUtc)
    {
        EnsurePending();
        Status = GroupJoinRequestStatus.Approved;
        RespondedByUserId = actorUserId;
        RespondedAtUtc = respondedAtUtc;
    }

    public void Decline(Guid actorUserId, DateTimeOffset respondedAtUtc)
    {
        EnsurePending();
        Status = GroupJoinRequestStatus.Declined;
        RespondedByUserId = actorUserId;
        RespondedAtUtc = respondedAtUtc;
    }

    private void EnsurePending()
    {
        if (Status != GroupJoinRequestStatus.Pending)
        {
            throw new InvalidOperationException("Only pending group join requests can be changed.");
        }
    }
}
