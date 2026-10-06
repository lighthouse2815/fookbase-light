using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Pages.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Pages.Entities;

[PrimaryKey(nameof(PageId), nameof(Slot))]
[Index(nameof(MediaId))]
public sealed class PageMediaReference
{
    private PageMediaReference()
    {
    }

    public PageMediaReference(Guid pageId, PageMediaSlot slot, Guid mediaId, DateTimeOffset attachedAtUtc)
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

    [InverseProperty(nameof(Page.MediaReferences))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Page Page { get; private set; } = null!;

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset Media { get; private set; } = null!;
}
