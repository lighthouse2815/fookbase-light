namespace Fookbase.Contracts.Identity;

public sealed record UserRegisteredIntegrationEvent(
    Guid EventId,
    Guid UserId,
    string Username,
    DateTimeOffset OccurredAtUtc)
{
    public const string EventType = "identity.user.registered.v1";

    public const string ExchangeName = "fookbase.identity.events";

    public const string RoutingKey = "identity.user.registered.v1";

    public const string QueueName = "fookbase.users.user-registered.v1";

    public const string FriendsQueueName = "fookbase.friends.user-registered.v1";
}
