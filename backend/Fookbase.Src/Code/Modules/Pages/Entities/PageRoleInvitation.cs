namespace Fookbase.Api.Modules.Pages.Entities;

public enum PageRoleInvitationStatus
{
    PENDING,
    ACCEPTED,
    DECLINED,
    CANCELLED
}

public sealed class PageRoleInvitation
{
    private PageRoleInvitation()
    {
    }

    private PageRoleInvitation(Guid id, Guid pageId, Guid inviterUserId, Guid inviteeUserId, PageRole role,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        PageId = pageId;
        InviterUserId = inviterUserId;
        InviteeUserId = inviteeUserId;
        Role = role;
        Status = PageRoleInvitationStatus.PENDING;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid PageId { get; private set; }
    public Guid InviterUserId { get; private set; }
    public Guid InviteeUserId { get; private set; }
    public PageRole Role { get; private set; }
    public PageRoleInvitationStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? RespondedAtUtc { get; private set; }

    public static PageRoleInvitation Create(Guid id, Guid pageId, Guid inviterUserId, Guid inviteeUserId,
        PageRole role, DateTimeOffset createdAtUtc) => new(id, pageId, inviterUserId, inviteeUserId, role, createdAtUtc);

    public void Accept(DateTimeOffset respondedAtUtc) => Respond(PageRoleInvitationStatus.ACCEPTED, respondedAtUtc);
    public void Decline(DateTimeOffset respondedAtUtc) => Respond(PageRoleInvitationStatus.DECLINED, respondedAtUtc);

    private void Respond(PageRoleInvitationStatus status, DateTimeOffset respondedAtUtc)
    {
        if (Status != PageRoleInvitationStatus.PENDING)
        {
            throw new InvalidOperationException("Only pending page role invitations can be responded to.");
        }

        Status = status;
        RespondedAtUtc = respondedAtUtc;
    }
}
