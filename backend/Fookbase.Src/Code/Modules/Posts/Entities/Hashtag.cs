namespace Fookbase.Api.Modules.Posts.Entities;

public sealed class Hashtag
{
    public const int MaximumLength = 50;

    private Hashtag()
    {
    }

    private Hashtag(Guid id, string normalizedName, string displayName, DateTimeOffset createdAtUtc)
    {
        Id = id;
        NormalizedName = normalizedName;
        DisplayName = displayName;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string NormalizedName { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Hashtag Create(Guid id, string normalizedName, string displayName, DateTimeOffset createdAtUtc) =>
        new(id, normalizedName, displayName, createdAtUtc);
}
