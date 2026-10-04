using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Media.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Stories.Entities;

[Table("StoryMediaReferences")]
[Index(nameof(MediaId))]
public sealed class StoryMediaReference
{
    private StoryMediaReference() { }

    private StoryMediaReference(Guid storyId, Guid mediaId, DateTimeOffset attachedAtUtc)
    {
        StoryId = storyId;
        MediaId = mediaId;
        AttachedAtUtc = attachedAtUtc;
    }

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Guid StoryId { get; private set; }
    public Guid MediaId { get; private set; }
    public DateTimeOffset AttachedAtUtc { get; private set; }

    [ForeignKey(nameof(StoryId))]
    [InverseProperty(nameof(Story.MediaReference))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Story Story { get; private set; } = null!;

    [ForeignKey(nameof(MediaId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset Media { get; private set; } = null!;

    public static StoryMediaReference Create(Guid storyId, Guid mediaId, DateTimeOffset attachedAtUtc) =>
        new(storyId, mediaId, attachedAtUtc);
}
