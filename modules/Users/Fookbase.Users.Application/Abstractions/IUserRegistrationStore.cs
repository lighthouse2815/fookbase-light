using Fookbase.Contracts.Identity;
using Fookbase.Users.Domain.Entities;

namespace Fookbase.Users.Application.Abstractions;

public interface IUserRegistrationStore
{
    Task<bool> CreateProfileOnceAsync(
        UserRegisteredIntegrationEvent integrationEvent,
        UserProfile profile,
        CancellationToken cancellationToken = default);
}
