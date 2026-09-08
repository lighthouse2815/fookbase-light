using Fookbase.Api.Shared.Contracts.Identity;
using Fookbase.Api.Modules.Users.Services;
using Fookbase.Api.Modules.Users.Models;

namespace Fookbase.Api.Modules.Users.Services;

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
