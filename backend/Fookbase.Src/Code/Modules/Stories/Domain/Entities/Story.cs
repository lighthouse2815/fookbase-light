using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Stories.Entities;

[Table("Stories")]
[Index(nameof(AuthorUserId), nameof(ExpiresAtUtc), nameof(CreatedAtUtc), nameof(Id))]
[Index(nameof(ExpiresAtUtc), nameof(CreatedAtUtc))]
[Index(nameof(MediaId))]
public sealed class Story
{
    public const int MaximumCaptionLength = 2_200;

    private Story() { }

    private Story(
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
        Caption = NormalizeCaption(caption);
        Privacy = privacy;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    [Key]
    public Guid Id { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public Guid MediaId { get; private set; }
    [MaxLength(MaximumCaptionLength)]
    public string? Caption { get; private set; }
    public PostPrivacy Privacy { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    [ForeignKey(nameof(AuthorUserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User AuthorUser { get; private set; } = null!;

    [ForeignKey(nameof(MediaId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset Media { get; private set; } = null!;

    public StoryMediaReference? MediaReference { get; private set; }

    public ICollection<StoryView> Views { get; private set; } = new List<StoryView>();

    public ICollection<StoryReaction> Reactions { get; private set; } = new List<StoryReaction>();

    public static Story Create(
        Guid id,
        Guid authorUserId,
        Guid mediaId,
        string? caption,
        PostPrivacy privacy,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc) =>
        new(id, authorUserId, mediaId, caption, privacy, createdAtUtc, expiresAtUtc);

    public void Delete(DateTimeOffset deletedAtUtc)
    {
        if (DeletedAtUtc is null)
        {
            DeletedAtUtc = deletedAtUtc;
        }
    }

    private static string? NormalizeCaption(string? caption)
    {
        var normalized = caption?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > MaximumCaptionLength)
        {
            throw new ArgumentException($"Story caption cannot exceed {MaximumCaptionLength} characters.");
        }

        return normalized;
    }
}
