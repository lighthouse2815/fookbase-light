using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Posts.Common;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Entities;

[Index(nameof(OriginalPostId), nameof(DeletedAtUtc))]
[Index(nameof(SharingUserId), nameof(DeletedAtUtc), nameof(CreatedAtUtc), nameof(OriginalPostId))]
[Index(nameof(DestinationType), nameof(DestinationId), nameof(DeletedAtUtc), nameof(CreatedAtUtc), nameof(Id))]
public sealed class PostShare
{
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
        Caption = PostShareNormalization.NormalizeCaption(caption);
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid OriginalPostId { get; private set; }

    [InverseProperty(nameof(Post.Shares))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Post OriginalPost { get; private set; } = null!;

    public Guid SharingUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User SharingUser { get; private set; } = null!;

    public PostShareDestinationType DestinationType { get; private set; }

    public Guid DestinationId { get; private set; }

    [MaxLength(10_000)]
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
}
