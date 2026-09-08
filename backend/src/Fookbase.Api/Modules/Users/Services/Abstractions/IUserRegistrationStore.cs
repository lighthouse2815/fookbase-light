using Fookbase.Api.Shared.Contracts.Identity;
using Fookbase.Api.Modules.Users.Entities;

namespace Fookbase.Api.Modules.Users.Services.Abstractions;

public interface IUserRegistrationStore
{
    Task<bool> CreateProfileOnceAsync(
        UserRegisteredIntegrationEvent integrationEvent,
        UserProfile profile,
        CancellationToken cancellationToken = default);
}
