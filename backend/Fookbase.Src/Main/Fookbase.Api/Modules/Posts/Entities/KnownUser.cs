namespace Fookbase.Api.Modules.Posts.Entities;

public sealed class KnownUser
{
    private KnownUser()
    {
    }

    private KnownUser(Guid userId, DateTimeOffset createdAtUtc)
    {
        UserId = userId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid UserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static KnownUser Create(Guid userId, DateTimeOffset createdAtUtc) =>
        new(userId, createdAtUtc);
}
