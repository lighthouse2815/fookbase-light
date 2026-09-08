namespace Fookbase.Api.Shared.Contracts.Identity;

public sealed record UserRegisteredIntegrationEvent(
    Guid EventId,
    Guid UserId,
    string Username,
    DateTimeOffset OccurredAtUtc)
{
    public const string EventType = "identity.user.registered.v1";
}
