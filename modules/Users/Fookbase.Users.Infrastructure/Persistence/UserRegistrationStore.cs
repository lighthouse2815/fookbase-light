using Fookbase.Contracts.Identity;
using Fookbase.Users.Application.Abstractions;
using Fookbase.Users.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Users.Infrastructure.Persistence;

internal sealed class UserRegistrationStore(
    UsersDbContext dbContext,
    TimeProvider timeProvider) : IUserRegistrationStore
{
    public async Task<bool> CreateProfileOnceAsync(
        UserRegisteredIntegrationEvent integrationEvent,
        UserProfile profile,
        CancellationToken cancellationToken = default)
    {
        if (await dbContext.InboxMessages.AnyAsync(
                message => message.EventId == integrationEvent.EventId,
                cancellationToken))
        {
            return false;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        dbContext.UserProfiles.Add(profile);
        dbContext.InboxMessages.Add(
            InboxMessage.Create(
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
                    cancellationToken))
            {
                return false;
            }

            throw;
        }
    }
}
