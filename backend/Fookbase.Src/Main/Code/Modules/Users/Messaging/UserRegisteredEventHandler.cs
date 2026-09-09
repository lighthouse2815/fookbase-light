using Fookbase.Api.Shared.Contracts.Identity;
using Fookbase.Api.Modules.Users.Data;
using Fookbase.Api.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Users.Messaging;

public sealed class UserRegisteredEventHandler(
    UsersDbContext dbContext,
    TimeProvider timeProvider)
{
    public Task<bool> HandleAsync(
        UserRegisteredIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        var profile = UserProfile.Create(
            integrationEvent.UserId,
            integrationEvent.Username,
            integrationEvent.OccurredAtUtc);

        return CreateProfileOnceAsync(integrationEvent, profile, cancellationToken);
    }

    private async Task<bool> CreateProfileOnceAsync(
        UserRegisteredIntegrationEvent integrationEvent,
        UserProfile profile,
        CancellationToken cancellationToken)
    {
        if (await dbContext.InboxMessages.AnyAsync(
                message => message.EventId == integrationEvent.EventId,
                cancellationToken)) return false;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.UserProfiles.Add(profile);
        dbContext.InboxMessages.Add(InboxMessage.Create(
            integrationEvent.EventId,
            UserRegisteredIntegrationEvent.EventType,
            timeProvider.GetUtcNow()));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            if (await dbContext.InboxMessages.AnyAsync(
                    message => message.EventId == integrationEvent.EventId,
                    cancellationToken)) return false;
            throw;
        }
    }
}
