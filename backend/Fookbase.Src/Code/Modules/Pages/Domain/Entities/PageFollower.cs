using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Pages.Entities;

[PrimaryKey(nameof(PageId), nameof(UserId))]
[Index(nameof(UserId), nameof(FollowedAtUtc))]
public sealed class PageFollower
{
    private PageFollower()
    {
    }

    public PageFollower(Guid pageId, Guid userId, DateTimeOffset followedAtUtc)
    {
        PageId = pageId;
        UserId = userId;
        FollowedAtUtc = followedAtUtc;
    }

    public Guid PageId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset FollowedAtUtc { get; private set; }

    [InverseProperty(nameof(Page.Followers))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Page Page { get; private set; } = null!;

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User User { get; private set; } = null!;
}
