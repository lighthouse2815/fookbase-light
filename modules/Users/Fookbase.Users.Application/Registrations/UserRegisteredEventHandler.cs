using Fookbase.Contracts.Identity;
using Fookbase.Users.Application.Abstractions;
using Fookbase.Users.Domain.Entities;

namespace Fookbase.Users.Application.Registrations;

public sealed class UserRegisteredEventHandler(IUserRegistrationStore registrationStore)
    : IUserRegisteredEventHandler
{
    public Task<bool> HandleAsync(
        UserRegisteredIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        var profile = UserProfile.Create(
            integrationEvent.UserId,
            integrationEvent.Username,
            integrationEvent.OccurredAtUtc);

        return registrationStore.CreateProfileOnceAsync(
            integrationEvent,
            profile,
            cancellationToken);
    }
}
