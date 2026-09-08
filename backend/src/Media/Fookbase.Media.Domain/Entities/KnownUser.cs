namespace Fookbase.Media.Domain.Entities;

public sealed class KnownUser
{
    private KnownUser() { }

    private KnownUser(Guid userId, string username, DateTimeOffset registeredAtUtc)
    {
        UserId = userId;
        Username = username;
        RegisteredAtUtc = registeredAtUtc;
    }

    public Guid UserId { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public DateTimeOffset RegisteredAtUtc { get; private set; }

    public static KnownUser Create(Guid userId, string username, DateTimeOffset registeredAtUtc) =>
        new(userId, username, registeredAtUtc);
}
