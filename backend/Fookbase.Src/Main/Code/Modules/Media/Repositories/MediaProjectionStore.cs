using Fookbase.Api.Shared.Contracts.Identity;
using Fookbase.Api.Shared.Contracts.Posts;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Media.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Media.Repositories;

public sealed class MediaProjectionStore(MediaDbContext db, TimeProvider timeProvider)
{
    public Task<bool> ProjectAsync(UserRegisteredIntegrationEvent message, CancellationToken token = default) =>
        ProcessAsync(message.EventId, UserRegisteredIntegrationEvent.EventType, async () =>
        {
            if (!await db.KnownUsers.AnyAsync(x => x.UserId == message.UserId, token))
                db.KnownUsers.Add(KnownUser.Create(message.UserId, message.Username, message.OccurredAtUtc));
        }, token);

    public Task<bool> ProjectAsync(PostMediaAttachedIntegrationEvent message, CancellationToken token = default) =>
        ProcessAsync(message.EventId, PostMediaAttachedIntegrationEvent.EventType, async () =>
        {
            if (!await db.MediaReferences.AnyAsync(
                    x => x.MediaId == message.MediaId && x.PostId == message.PostId, token))
                db.MediaReferences.Add(MediaReference.Create(message.MediaId, message.PostId, message.OccurredAtUtc));
        }, token);

    public Task<bool> ProjectAsync(PostMediaDetachedIntegrationEvent message, CancellationToken token = default) =>
        ProcessAsync(message.EventId, PostMediaDetachedIntegrationEvent.EventType, async () =>
        {
            var reference = await db.MediaReferences.SingleOrDefaultAsync(
                x => x.MediaId == message.MediaId && x.PostId == message.PostId, token);
            if (reference is not null) db.MediaReferences.Remove(reference);
        }, token);

    private async Task<bool> ProcessAsync(Guid id, string type, Func<Task> change, CancellationToken token)
    {
        if (await db.InboxMessages.AnyAsync(x => x.EventId == id, token)) return false;
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await change();
        db.InboxMessages.Add(InboxMessage.Create(id, type, timeProvider.GetUtcNow()));
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return true;
    }
}
