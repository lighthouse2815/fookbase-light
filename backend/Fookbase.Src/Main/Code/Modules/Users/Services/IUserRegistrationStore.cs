using Fookbase.Api.Shared.Contracts.Identity;
using Fookbase.Api.Modules.Users.Models;

namespace Fookbase.Api.Modules.Users.Services;

public interface IUserRegistrationStore
{
    Task<bool> CreateProfileOnceAsync(
        UserRegisteredIntegrationEvent integrationEvent,
        UserProfile profile,
        CancellationToken cancellationToken = default);
}
