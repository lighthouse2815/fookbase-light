namespace Fookbase.Api.Modules.Pages.Entities;

public enum PageRole
{
    Owner,
    Admin,
    Editor,
    Moderator
}

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

    public static PageMember Create(Guid pageId, Guid userId, PageRole role, DateTimeOffset joinedAtUtc) =>
        new(pageId, userId, role, joinedAtUtc);

    public void ChangeRole(PageRole role, DateTimeOffset updatedAtUtc)
    {
        Role = role;
        UpdatedAtUtc = updatedAtUtc;
    }
}
