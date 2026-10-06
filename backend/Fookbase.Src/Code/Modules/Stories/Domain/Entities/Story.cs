using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Modules.Stories.Common;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Stories.Entities;

[Index(nameof(AuthorUserId), nameof(ExpiresAtUtc), nameof(CreatedAtUtc), nameof(Id))]
[Index(nameof(ExpiresAtUtc), nameof(CreatedAtUtc))]
[Index(nameof(MediaId))]
public sealed class Story
{
    public const int MaximumCaptionLength = 2_200;

    private Story() { }

    public Story(
        Guid id,
        Guid authorUserId,
        Guid mediaId,
        string? caption,
        PostPrivacy privacy,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        Id = id;
        AuthorUserId = authorUserId;
        MediaId = mediaId;
        Caption = StoryNormalization.NormalizeCaption(caption);
        Privacy = privacy;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    [Key]
    public Guid Id { get; private set; }

    public Guid AuthorUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User AuthorUser { get; private set; } = null!;

    public Guid MediaId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset Media { get; private set; } = null!;

    [MaxLength(MaximumCaptionLength)]
    public string? Caption { get; private set; }

    public PostPrivacy Privacy { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public StoryMediaReference? MediaReference { get; private set; }

    public ICollection<StoryView> Views { get; private set; } = new List<StoryView>();

    public ICollection<StoryReaction> Reactions { get; private set; } = new List<StoryReaction>();

    public void Delete(DateTimeOffset deletedAtUtc)
    {
        if (DeletedAtUtc is null)
        {
            DeletedAtUtc = deletedAtUtc;
        }
    }
}
