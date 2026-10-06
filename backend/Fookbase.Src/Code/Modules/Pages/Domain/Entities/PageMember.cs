using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Pages.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Pages.Entities;

[PrimaryKey(nameof(PageId), nameof(UserId))]
[Index(nameof(UserId), nameof(PageId))]
[Index(nameof(PageId), nameof(Role))]
public sealed class PageMember
{
    private PageMember()
    {
    }

    private PageMember(Guid pageId, Guid userId, PageRole role, DateTimeOffset joinedAtUtc)
    {
        PageId = pageId;
        UserId = userId;
        Role = role;
        JoinedAtUtc = joinedAtUtc;
    }

    public Guid PageId { get; private set; }
    public Guid UserId { get; private set; }
    public PageRole Role { get; private set; }
    public DateTimeOffset JoinedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    [InverseProperty(nameof(Page.Members))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Page Page { get; private set; } = null!;

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User User { get; private set; } = null!;

    public static PageMember Create(Guid pageId, Guid userId, PageRole role, DateTimeOffset joinedAtUtc) =>
        new(pageId, userId, role, joinedAtUtc);

    public void ChangeRole(PageRole role, DateTimeOffset updatedAtUtc)
    {
        Role = role;
        UpdatedAtUtc = updatedAtUtc;
    }
}
