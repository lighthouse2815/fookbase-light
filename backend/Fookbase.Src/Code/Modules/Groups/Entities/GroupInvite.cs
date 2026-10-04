namespace Fookbase.Api.Modules.Groups.Entities;

public enum GroupInviteStatus
{
    PENDING,
    ACCEPTED,
    DECLINED
}

public sealed class GroupInvite
{
    private GroupInvite()
    {
    }

    private GroupInvite(
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

    public Guid Id { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid InviterUserId { get; private set; }
    public Guid InviteeUserId { get; private set; }
    public GroupInviteStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? RespondedAtUtc { get; private set; }

    public static GroupInvite Create(
        Guid id,
        Guid groupId,
        Guid inviterUserId,
        Guid inviteeUserId,
        DateTimeOffset createdAtUtc) =>
        new(id, groupId, inviterUserId, inviteeUserId, createdAtUtc);

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
