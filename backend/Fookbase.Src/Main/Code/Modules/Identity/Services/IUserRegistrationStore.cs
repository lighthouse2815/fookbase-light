using Fookbase.Api.Shared.Contracts.Identity;
using Fookbase.Api.Modules.Identity.Models;

namespace Fookbase.Api.Modules.Identity.Services;

public interface IUserRegistrationStore
{
    Task<UserCreationResult> CreateAsync(
        User user,
        string password,
        RefreshToken refreshToken,
        UserRegisteredIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default);
}
