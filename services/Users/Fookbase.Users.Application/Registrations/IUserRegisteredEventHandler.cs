using Fookbase.Contracts.Identity;

namespace Fookbase.Users.Application.Registrations;

public interface IUserRegisteredEventHandler
{
    Task<bool> HandleAsync(
        UserRegisteredIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default);
}
