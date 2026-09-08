using Fookbase.Contracts.Identity;
using Fookbase.Identity.Domain.Entities;

namespace Fookbase.Identity.Application.Abstractions;

public interface IUserRegistrationStore
{
    Task<UserCreationResult> CreateAsync(
        User user,
        string password,
        RefreshToken refreshToken,
        UserRegisteredIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default);
}
