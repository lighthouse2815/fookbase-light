using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Entities;

[Table("Hashtags")]
[Index(nameof(NormalizedName), IsUnique = true)]
public sealed class Hashtag
{
    public const int MaximumLength = 50;

    private Hashtag()
    {
    }

    public Hashtag(Guid id, string normalizedName, string displayName, DateTimeOffset createdAtUtc)
    {
        Id = id;
        NormalizedName = normalizedName;
        DisplayName = displayName;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    [Required]
    [MaxLength(MaximumLength)]
    public string NormalizedName { get; private set; } = string.Empty;

    [Required]
    [MaxLength(MaximumLength)]
    public string DisplayName { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

}
