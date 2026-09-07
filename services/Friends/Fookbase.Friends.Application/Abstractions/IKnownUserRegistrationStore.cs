using Fookbase.Contracts.Identity;
using Fookbase.Friends.Domain.Entities;

namespace Fookbase.Friends.Application.Abstractions;

public interface IKnownUserRegistrationStore
{
    Task<bool> ProjectOnceAsync(
        UserRegisteredIntegrationEvent integrationEvent,
        KnownUser knownUser,
        CancellationToken cancellationToken = default);
}
