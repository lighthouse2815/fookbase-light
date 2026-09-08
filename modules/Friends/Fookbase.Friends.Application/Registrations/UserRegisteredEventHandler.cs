using Fookbase.Contracts.Identity;
using Fookbase.Friends.Application.Abstractions;
using Fookbase.Friends.Domain.Entities;

namespace Fookbase.Friends.Application.Registrations;

public sealed class UserRegisteredEventHandler(IKnownUserRegistrationStore registrationStore)
    : IUserRegisteredEventHandler
{
    public Task<bool> HandleAsync(
        UserRegisteredIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default) =>
        registrationStore.ProjectOnceAsync(
            integrationEvent,
            KnownUser.Create(
                integrationEvent.UserId,
                integrationEvent.Username,
                integrationEvent.OccurredAtUtc),
            cancellationToken);
}
