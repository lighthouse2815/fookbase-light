using Fookbase.Api.Shared.Contracts.Identity;
using Fookbase.Api.Modules.Identity.Entities;

namespace Fookbase.Api.Modules.Identity.Services.Abstractions;

public interface IUserRegistrationStore
{
    Task<UserCreationResult> CreateAsync(
        User user,
        string password,
        RefreshToken refreshToken,
        UserRegisteredIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default);
}
