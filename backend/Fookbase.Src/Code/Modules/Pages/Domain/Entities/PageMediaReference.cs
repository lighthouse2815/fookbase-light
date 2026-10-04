using Fookbase.Api.Modules.Pages.Domain.Enums;
namespace Fookbase.Api.Modules.Pages.Entities;

public sealed class PageMediaReference
{
    private PageMediaReference()
    {
    }

    private PageMediaReference(Guid pageId, PageMediaSlot slot, Guid mediaId, DateTimeOffset attachedAtUtc)
    {
        PageId = pageId;
        Slot = slot;
        MediaId = mediaId;
        AttachedAtUtc = attachedAtUtc;
    }

    public Guid PageId { get; private set; }
    public PageMediaSlot Slot { get; private set; }
    public Guid MediaId { get; private set; }
    public DateTimeOffset AttachedAtUtc { get; private set; }

    public static PageMediaReference Create(Guid pageId, PageMediaSlot slot, Guid mediaId, DateTimeOffset attachedAtUtc) =>
        new(pageId, slot, mediaId, attachedAtUtc);
}
