namespace Fookbase.Api.Modules.Messages.Entities;

public sealed class Conversation
{
    private Conversation()
    {
    }

    private Conversation(
        Guid id,
        Guid userId1,
        Guid userId2,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        UserId1 = userId1;
        UserId2 = userId2;
        CreatedAtUtc = createdAtUtc;
        LastMessageAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid UserId1 { get; private set; }

    public Guid UserId2 { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset LastMessageAtUtc { get; private set; }

    public static Conversation Create(
        Guid id,
        Guid firstUserId,
        Guid secondUserId,
        DateTimeOffset createdAtUtc)
    {
        if (firstUserId == secondUserId)
        {
            throw new ArgumentException("A conversation requires two different users.");
        }

        return firstUserId.CompareTo(secondUserId) < 0
            ? new Conversation(id, firstUserId, secondUserId, createdAtUtc)
            : new Conversation(id, secondUserId, firstUserId, createdAtUtc);
    }

    public bool Contains(Guid userId) => UserId1 == userId || UserId2 == userId;

    public Guid OtherUserId(Guid userId) =>
        UserId1 == userId
            ? UserId2
            : UserId2 == userId
                ? UserId1
                : throw new UnauthorizedAccessException("The user is not part of this conversation.");

    public void RecordMessage(DateTimeOffset createdAtUtc) => LastMessageAtUtc = createdAtUtc;
}
