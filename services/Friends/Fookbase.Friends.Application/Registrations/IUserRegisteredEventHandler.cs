using Fookbase.Contracts.Identity;

namespace Fookbase.Friends.Application.Registrations;

public interface IUserRegisteredEventHandler
{
    Task<bool> HandleAsync(
        UserRegisteredIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default);
}
