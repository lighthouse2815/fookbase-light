using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Entities;

[Index(nameof(OriginalPostId), nameof(DeletedAtUtc))]
[Index(nameof(SharingUserId), nameof(DeletedAtUtc), nameof(CreatedAtUtc), nameof(OriginalPostId))]
[Index(nameof(DestinationType), nameof(DestinationId), nameof(DeletedAtUtc), nameof(CreatedAtUtc), nameof(Id))]
public sealed class PostShare
{
    public const int MaximumCaptionLength = Post.MaximumContentLength;

    private PostShare() { }

    public PostShare(
        Guid id,
        Guid originalPostId,
        Guid sharingUserId,
        PostShareDestinationType destinationType,
        Guid destinationId,
        string? caption,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        OriginalPostId = originalPostId;
        SharingUserId = sharingUserId;
        DestinationType = destinationType;
        DestinationId = destinationId;
        Caption = NormalizeCaption(caption);
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid OriginalPostId { get; private set; }

    [ForeignKey(nameof(OriginalPostId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Post OriginalPost { get; private set; } = null!;

    public Guid SharingUserId { get; private set; }

    public PostShareDestinationType DestinationType { get; private set; }

    public Guid DestinationId { get; private set; }

    [MaxLength(MaximumCaptionLength)]
    public string? Caption { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public void Delete(DateTimeOffset deletedAtUtc)
    {
        if (DeletedAtUtc is not null)
        {
            return;
        }

        DeletedAtUtc = deletedAtUtc;
    }

    private static string? NormalizeCaption(string? caption)
    {
        if (string.IsNullOrWhiteSpace(caption))
        {
            return null;
        }

        var normalized = caption.Trim();
        if (normalized.Length > MaximumCaptionLength)
        {
            throw new ArgumentException($"Share caption cannot exceed {MaximumCaptionLength} characters.");
        }

        return normalized;
    }
}
