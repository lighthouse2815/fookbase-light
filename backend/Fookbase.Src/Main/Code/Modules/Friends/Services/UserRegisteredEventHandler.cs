using Fookbase.Api.Shared.Contracts.Identity;
using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Friends.Models;

namespace Fookbase.Api.Modules.Friends.Services;

public sealed class UserRegisteredEventHandler(IKnownUserRegistrationStore registrationStore)
    : IUserRegisteredEventHandler
{
    public Task<bool> HandleAsync(
        UserRegisteredIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default) =>
        registrationStore.ProjectOnceAsync(
            integrationEvent,
            KnownUser.Create(
                integrationEvent.UserId,
                integrationEvent.Username,
                integrationEvent.OccurredAtUtc),
            cancellationToken);
}
