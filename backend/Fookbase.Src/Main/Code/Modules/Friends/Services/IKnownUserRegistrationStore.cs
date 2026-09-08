using Fookbase.Api.Shared.Contracts.Identity;
using Fookbase.Api.Modules.Friends.Models;

namespace Fookbase.Api.Modules.Friends.Services;

public interface IKnownUserRegistrationStore
{
    Task<bool> ProjectOnceAsync(
        UserRegisteredIntegrationEvent integrationEvent,
        KnownUser knownUser,
        CancellationToken cancellationToken = default);
}
