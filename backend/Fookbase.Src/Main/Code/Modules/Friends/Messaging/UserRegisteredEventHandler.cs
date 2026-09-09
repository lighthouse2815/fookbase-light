using Fookbase.Api.Shared.Contracts.Identity;
using Fookbase.Api.Modules.Friends.Data;
using Fookbase.Api.Modules.Friends.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Friends.Messaging;

public sealed class UserRegisteredEventHandler(
    FriendsDbContext dbContext,
    TimeProvider timeProvider)
{
    public async Task<bool> HandleAsync(
        UserRegisteredIntegrationEvent integrationEvent,
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
            $"SELECT pg_advisory_xact_lock(hashtextextended({integrationEvent.UserId.ToString("N")}, 1))",
            cancellationToken);

        if (await dbContext.InboxMessages.AnyAsync(
                message => message.EventId == integrationEvent.EventId,
                cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        if (!await dbContext.KnownUsers.AnyAsync(
                user => user.UserId == integrationEvent.UserId,
                cancellationToken))
        {
            dbContext.KnownUsers.Add(KnownUser.Create(
                integrationEvent.UserId,
                integrationEvent.Username,
                integrationEvent.OccurredAtUtc));
        }

        dbContext.InboxMessages.Add(InboxMessage.Create(
            integrationEvent.EventId,
            UserRegisteredIntegrationEvent.EventType,
            timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
