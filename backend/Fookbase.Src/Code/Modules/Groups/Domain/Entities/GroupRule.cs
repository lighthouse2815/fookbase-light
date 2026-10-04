namespace Fookbase.Api.Modules.Groups.Entities;

public sealed class GroupRule
{
    public const int MaximumTitleLength = 200;
    public const int MaximumDescriptionLength = 2_000;

    private GroupRule()
    {
    }

    public GroupRule(Guid id, Guid groupId, string title, string? description, int sortOrder)
    {
        Id = id;
        GroupId = groupId;
        Title = NormalizeTitle(title);
        Description = NormalizeDescription(description);
        SortOrder = sortOrder;
    }

    public Guid Id { get; private set; }
    public Guid GroupId { get; private set; }
    public string Title { get; private set; } = string.Empty;
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
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > MaximumDescriptionLength)
        {
            throw new ArgumentException(
                $"Group rule description cannot exceed {MaximumDescriptionLength} characters.");
        }

        return normalized;
    }
}
