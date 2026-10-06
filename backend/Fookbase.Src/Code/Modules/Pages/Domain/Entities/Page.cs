using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Pages.Common;
using Fookbase.Api.Modules.Pages.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Pages.Entities;

[Index(nameof(Username), IsUnique = true)]
[Index(nameof(Status), nameof(Name), nameof(Id))]
[Index(nameof(CreatedByUserId), nameof(CreatedAtUtc))]
public sealed class Page
{
    public const int MinimumUsernameLength = 3;
    public const int MaximumUsernameLength = 50;
    public const int MaximumNameLength = 120;
    public const int MaximumCategoryLength = 80;
    public const int MaximumBioLength = 2_000;

    private Page()
    {
    }

    public Page(Guid id, string name, string username, string category, string? bio, Guid createdByUserId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Name = PageNormalization.NormalizeName(name);
        Username = PageNormalization.NormalizeUsername(username);
        Category = PageNormalization.NormalizeCategory(category);
        Bio = PageNormalization.NormalizeBio(bio);
        CreatedByUserId = createdByUserId;
        Status = PageStatus.UNPUBLISHED;
        CreatedAtUtc = createdAtUtc;
    }

    [Key]
    public Guid Id { get; private set; }
    [Required]
    [MaxLength(MaximumNameLength)]
    public string Name { get; private set; } = string.Empty;
    [Required]
    [MaxLength(MaximumUsernameLength)]
    [Column(TypeName = "citext")]
    public string Username { get; private set; } = string.Empty;
    [Required]
    [MaxLength(MaximumCategoryLength)]
    public string Category { get; private set; } = string.Empty;
    [MaxLength(MaximumBioLength)]
    public string? Bio { get; private set; }
    public Guid? AvatarMediaId { get; private set; }
    public Guid? CoverMediaId { get; private set; }
    public PageStatus Status { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User CreatedByUser { get; private set; } = null!;

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset? AvatarMedia { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset? CoverMedia { get; private set; }

    public ICollection<PageMember> Members { get; private set; } = new List<PageMember>();
    public ICollection<PageFollower> Followers { get; private set; } = new List<PageFollower>();
    public ICollection<PageRoleInvitation> RoleInvitations { get; private set; } = new List<PageRoleInvitation>();
    public ICollection<PageMediaReference> MediaReferences { get; private set; } = new List<PageMediaReference>();

    public void Update(string name, string username, string category, string? bio, DateTimeOffset updatedAtUtc)
    {
        EnsureActive();
        Name = PageNormalization.NormalizeName(name);
        Username = PageNormalization.NormalizeUsername(username);
        Category = PageNormalization.NormalizeCategory(category);
        Bio = PageNormalization.NormalizeBio(bio);
        UpdatedAtUtc = updatedAtUtc;
    }

    public void SetMedia(Guid? avatarMediaId, Guid? coverMediaId, DateTimeOffset updatedAtUtc)
    {
        EnsureActive();
        AvatarMediaId = avatarMediaId;
        CoverMediaId = coverMediaId;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Publish(DateTimeOffset updatedAtUtc)
    {
        EnsureActive();
        Status = PageStatus.PUBLISHED;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Unpublish(DateTimeOffset updatedAtUtc)
    {
        EnsureActive();
        Status = PageStatus.UNPUBLISHED;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Delete(DateTimeOffset deletedAtUtc)
    {
        EnsureActive();
        DeletedAtUtc = deletedAtUtc;
        AvatarMediaId = null;
        CoverMediaId = null;
        UpdatedAtUtc = deletedAtUtc;
    }

    private void EnsureActive()
    {
        if (DeletedAtUtc is not null)
        {
            throw new InvalidOperationException("A deleted page cannot be changed.");
        }
    }
}
