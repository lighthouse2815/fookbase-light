using Fookbase.Api.Shared.Contracts.Identity;
using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Friends.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Friends.Repositories;

internal sealed class KnownUserRegistrationStore(
    FriendsDbContext dbContext,
    TimeProvider timeProvider) : IKnownUserRegistrationStore
{
    public async Task<bool> ProjectOnceAsync(
        UserRegisteredIntegrationEvent integrationEvent,
        KnownUser knownUser,
        CancellationToken cancellationToken = default)
    {
        if (await dbContext.InboxMessages.AnyAsync(
                message => message.EventId == integrationEvent.EventId,
                cancellationToken))
        {
            return false;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({knownUser.UserId.ToString("N")}, 1))",
            cancellationToken);

        if (await dbContext.InboxMessages.AnyAsync(
                message => message.EventId == integrationEvent.EventId,
                cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        if (!await dbContext.KnownUsers.AnyAsync(
                user => user.UserId == knownUser.UserId,
                cancellationToken))
        {
            dbContext.KnownUsers.Add(knownUser);
        }

        dbContext.InboxMessages.Add(
            InboxMessage.Create(
                integrationEvent.EventId,
                UserRegisteredIntegrationEvent.EventType,
                timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
