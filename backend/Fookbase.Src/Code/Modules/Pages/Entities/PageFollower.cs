namespace Fookbase.Api.Modules.Pages.Entities;

public sealed class PageFollower
{
    private PageFollower()
    {
    }

    private PageFollower(Guid pageId, Guid userId, DateTimeOffset followedAtUtc)
    {
        PageId = pageId;
        UserId = userId;
        FollowedAtUtc = followedAtUtc;
    }

    public Guid PageId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset FollowedAtUtc { get; private set; }

    public static PageFollower Create(Guid pageId, Guid userId, DateTimeOffset followedAtUtc) =>
        new(pageId, userId, followedAtUtc);
}
