using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Groups.Entities;

[Index(nameof(GroupId), nameof(SortOrder), nameof(Id))]
public sealed class GroupRule
{
    public const int MaximumTitleLength = 200;
    public const int MaximumDescriptionLength = 2_000;

    private GroupRule() { }

    public GroupRule(Guid id, Guid groupId, string title, string? description, int sortOrder)
    {
        Id = id;
        GroupId = groupId;
        Title = NormalizeTitle(title);
        Description = NormalizeDescription(description);
        SortOrder = sortOrder;
    }

    [Key]
    public Guid Id { get; private set; }

    public Guid GroupId { get; private set; }

    [InverseProperty(nameof(Group.Rules))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Group Group { get; private set; } = null!;

    [Required]
    [MaxLength(MaximumTitleLength)]
    public string Title { get; private set; } = string.Empty;

    [MaxLength(MaximumDescriptionLength)]
    public string? Description { get; private set; }

    public int SortOrder { get; private set; }

    public void Update(string title, string? description, int sortOrder)
    {
        Title = NormalizeTitle(title);
        Description = NormalizeDescription(description);
        SortOrder = sortOrder;
    }

    private static string NormalizeTitle(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > MaximumTitleLength)
        {
            throw new ArgumentException(
                $"Group rule title must contain between 1 and {MaximumTitleLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeDescription(string? value)
    {
        var normalized = TextNormalization.NormalizeOptionalText(value);
        if (normalized?.Length > MaximumDescriptionLength)
        {
            throw new ArgumentException(
                $"Group rule description cannot exceed {MaximumDescriptionLength} characters.");
        }

        return normalized;
    }
}
