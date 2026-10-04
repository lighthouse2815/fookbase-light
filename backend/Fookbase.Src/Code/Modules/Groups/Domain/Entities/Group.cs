using Fookbase.Api.Modules.Groups.Domain.Enums;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Groups.Entities;

[Table("Groups")]
[Index(nameof(Privacy), nameof(Name))]
[Index(nameof(OwnerUserId), nameof(CreatedAtUtc))]
public sealed class Group
{
    public const int MaximumNameLength = 120;
    public const int MaximumDescriptionLength = 2_000;

    private Group()
    {
    }

    public Group(
        Guid id,
        string name,
        string? description,
        GroupPrivacy privacy,
        Guid ownerUserId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        Privacy = privacy;
        OwnerUserId = ownerUserId;
        CreatedAtUtc = createdAtUtc;
    }

    [Key]
    public Guid Id { get; private set; }
    [Required]
    [MaxLength(MaximumNameLength)]
    public string Name { get; private set; } = string.Empty;
    [MaxLength(MaximumDescriptionLength)]
    public string? Description { get; private set; }
    public GroupPrivacy Privacy { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid? CoverMediaId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    [ForeignKey(nameof(OwnerUserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User OwnerUser { get; private set; } = null!;

    [ForeignKey(nameof(CoverMediaId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset? CoverMedia { get; private set; }

    public ICollection<GroupMember> Members { get; private set; } = new List<GroupMember>();
    public ICollection<GroupJoinRequest> JoinRequests { get; private set; } = new List<GroupJoinRequest>();
    public ICollection<GroupInvite> Invites { get; private set; } = new List<GroupInvite>();
    public ICollection<GroupRule> Rules { get; private set; } = new List<GroupRule>();
    public GroupCoverMediaReference? CoverMediaReference { get; private set; }

    public void Update(string name, string? description, GroupPrivacy privacy, DateTimeOffset updatedAtUtc)
    {
        EnsureActive();
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        Privacy = privacy;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void SetCover(Guid? coverMediaId, DateTimeOffset updatedAtUtc)
    {
        EnsureActive();
        CoverMediaId = coverMediaId;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void TransferOwnership(Guid ownerUserId, DateTimeOffset updatedAtUtc)
    {
        EnsureActive();
        OwnerUserId = ownerUserId;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Delete(DateTimeOffset deletedAtUtc)
    {
        EnsureActive();
        DeletedAtUtc = deletedAtUtc;
        CoverMediaId = null;
        UpdatedAtUtc = deletedAtUtc;
    }

    private void EnsureActive()
    {
        if (DeletedAtUtc is not null)
        {
            throw new InvalidOperationException("A deleted group cannot be changed.");
        }
    }

    private static string NormalizeName(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > MaximumNameLength)
        {
            throw new ArgumentException(
                $"Group name must contain between 1 and {MaximumNameLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeDescription(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > MaximumDescriptionLength)
        {
            throw new ArgumentException(
                $"Group description cannot exceed {MaximumDescriptionLength} characters.");
        }

        return normalized;
    }
}
