using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Pages.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Pages.Entities;

[Index(nameof(PageId), nameof(InviteeUserId), IsUnique = true)]
[Index(nameof(InviteeUserId), nameof(Status), nameof(CreatedAtUtc))]
public sealed class PageRoleInvitation
{
    private PageRoleInvitation()
    {
    }

    public PageRoleInvitation(Guid id, Guid pageId, Guid inviterUserId, Guid inviteeUserId, PageRole role,
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

    [Key]
    public Guid Id { get; private set; }
    public Guid PageId { get; private set; }
    public Guid InviterUserId { get; private set; }
    public Guid InviteeUserId { get; private set; }
    public PageRole Role { get; private set; }
    public PageRoleInvitationStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? RespondedAtUtc { get; private set; }

    [InverseProperty(nameof(Page.RoleInvitations))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Page Page { get; private set; } = null!;

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User InviterUser { get; private set; } = null!;

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User InviteeUser { get; private set; } = null!;

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
