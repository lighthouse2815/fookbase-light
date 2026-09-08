using Fookbase.Api.Shared.Contracts.Identity;

namespace Fookbase.Api.Modules.Friends.Services.Registrations;

public interface IUserRegisteredEventHandler
{
    Task<bool> HandleAsync(
        UserRegisteredIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default);
}
