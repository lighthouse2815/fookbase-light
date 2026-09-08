using Fookbase.Api.Shared.Contracts.Identity;

namespace Fookbase.Api.Modules.Users.Services;

public interface IUserRegisteredEventHandler
{
    Task<bool> HandleAsync(
        UserRegisteredIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default);
}
