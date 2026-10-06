using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Entities;

[Index(nameof(NormalizedName), IsUnique = true)]
public sealed class Hashtag
{
    private Hashtag() { }

    public Hashtag(
        Guid id,
        string normalizedName,
        string displayName,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        NormalizedName = normalizedName;
        DisplayName = displayName;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    [Required]
    [MaxLength(50)]
    public string NormalizedName { get; private set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string DisplayName { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public ICollection<PostHashtag> Posts { get; private set; } = new List<PostHashtag>();
}
