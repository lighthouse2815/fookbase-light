namespace Fookbase.Api.Modules.Friends.Entities;

public sealed class KnownUser
{
    private KnownUser()
    {
    }

    private KnownUser(Guid userId, string username, DateTimeOffset createdAtUtc)
    {
        UserId = userId;
        Username = username;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid UserId { get; private set; }

    public string Username { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static KnownUser Create(Guid userId, string username, DateTimeOffset createdAtUtc) =>
        new(userId, username, createdAtUtc);
}
