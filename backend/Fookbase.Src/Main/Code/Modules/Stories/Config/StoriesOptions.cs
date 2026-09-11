namespace Fookbase.Api.Modules.Stories.Config;

public sealed class StoriesOptions
{
    public const string SectionName = "Stories";

    public int LifetimeHours { get; init; } = 24;

    public void Validate()
    {
        if (LifetimeHours is < 1 or > 168)
        {
            throw new InvalidOperationException("Story lifetime must be between one hour and seven days.");
        }
    }
}
