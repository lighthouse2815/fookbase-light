using Fookbase.Api.Shared.Contracts.Identity;
using Fookbase.Api.Modules.Users.Services.Abstractions;
using Fookbase.Api.Modules.Users.Entities;

namespace Fookbase.Api.Modules.Users.Services.Registrations;

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
