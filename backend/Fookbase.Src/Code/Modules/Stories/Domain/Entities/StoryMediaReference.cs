using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Media.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Stories.Entities;

[Index(nameof(MediaId))]
public sealed class StoryMediaReference
{
    private StoryMediaReference() { }

    public StoryMediaReference(Guid storyId, Guid mediaId, DateTimeOffset attachedAtUtc)
    {
        StoryId = storyId;
        MediaId = mediaId;
        AttachedAtUtc = attachedAtUtc;
    }

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Guid StoryId { get; private set; }

    [InverseProperty(nameof(Story.MediaReference))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Story Story { get; private set; } = null!;

    public Guid MediaId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset Media { get; private set; } = null!;

    public DateTimeOffset AttachedAtUtc { get; private set; }
}
