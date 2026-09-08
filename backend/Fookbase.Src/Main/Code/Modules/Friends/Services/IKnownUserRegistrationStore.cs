using Fookbase.Api.Shared.Contracts.Identity;
using Fookbase.Api.Modules.Friends.Entities;

namespace Fookbase.Api.Modules.Friends.Services;

public interface IKnownUserRegistrationStore
{
    Task<bool> ProjectOnceAsync(
        UserRegisteredIntegrationEvent integrationEvent,
        KnownUser knownUser,
        CancellationToken cancellationToken = default);
}
