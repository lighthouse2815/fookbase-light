using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Pages.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Pages.Entities;

[Index(nameof(Username), IsUnique = true)]
[Index(nameof(Status), nameof(Name), nameof(Id))]
[Index(nameof(CreatedByUserId), nameof(CreatedAtUtc))]
public sealed partial class Page
{
    public const int MinimumUsernameLength = 3;
    public const int MaximumUsernameLength = 50;
    public const int MaximumNameLength = 120;
    public const int MaximumCategoryLength = 80;
    public const int MaximumBioLength = 2_000;

    private Page()
    {
    }

    private Page(Guid id, string name, string username, string category, string? bio, Guid createdByUserId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Name = NormalizeName(name);
        Username = NormalizeUsername(username);
        Category = NormalizeCategory(category);
        Bio = NormalizeBio(bio);
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

    public static Page Create(Guid id, string name, string username, string category, string? bio,
        Guid createdByUserId, DateTimeOffset createdAtUtc) =>
        new(id, name, username, category, bio, createdByUserId, createdAtUtc);

    public void Update(string name, string username, string category, string? bio, DateTimeOffset updatedAtUtc)
    {
        EnsureActive();
        Name = NormalizeName(name);
        Username = NormalizeUsername(username);
        Category = NormalizeCategory(category);
        Bio = NormalizeBio(bio);
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

    public static string NormalizeUsername(string? username)
    {
        var normalized = username?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length is < MinimumUsernameLength or > MaximumUsernameLength ||
            !UsernamePattern().IsMatch(normalized))
        {
            throw new ArgumentException($"Page username must contain {MinimumUsernameLength}-{MaximumUsernameLength} lowercase letters, digits, dots, or underscores.");
        }

        return normalized;
    }

    private void EnsureActive()
    {
        if (DeletedAtUtc is not null)
        {
            throw new InvalidOperationException("A deleted page cannot be changed.");
        }
    }

    private static string NormalizeName(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > MaximumNameLength)
        {
            throw new ArgumentException($"Page name must contain between 1 and {MaximumNameLength} characters.");
        }

        return normalized;
    }

    private static string NormalizeCategory(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > MaximumCategoryLength)
        {
            throw new ArgumentException($"Page category must contain between 1 and {MaximumCategoryLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeBio(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > MaximumBioLength)
        {
            throw new ArgumentException($"Page bio cannot exceed {MaximumBioLength} characters.");
        }

        return normalized;
    }

    [GeneratedRegex("^[a-z0-9._]+$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();
}
