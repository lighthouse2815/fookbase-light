using Fookbase.Api.Modules.Photos.Domain.Enums;
using Fookbase.Api.Modules.Photos.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Photos.Services;

public sealed class PhotoAccessService(FookbaseDbContext db)
{
    public async Task<bool> CanViewAsync(PhotoAlbum album, Guid? viewerId, CancellationToken ct = default)
    {
        if (viewerId == album.OwnerUserId) return true;
        if (viewerId is null || await IsBlockedAsync(album.OwnerUserId, viewerId.Value, ct)) return false;
        return album.Privacy == PhotoAlbumPrivacy.PUBLIC ||
               album.Privacy == PhotoAlbumPrivacy.FRIENDS && await AreFriendsAsync(album.OwnerUserId, viewerId.Value, ct);
    }

    public Task<bool> IsBlockedAsync(Guid firstId, Guid secondId, CancellationToken ct = default) =>
        db.BlockedUsers.AsNoTracking().AnyAsync(item =>
            (item.BlockerUserId == firstId && item.BlockedAccountId == secondId) ||
            (item.BlockerUserId == secondId && item.BlockedAccountId == firstId), ct);

    private Task<bool> AreFriendsAsync(Guid firstId, Guid secondId, CancellationToken ct) =>
        db.Friendships.AsNoTracking().AnyAsync(item =>
            (item.User1Id == firstId && item.User2Id == secondId) ||
            (item.User1Id == secondId && item.User2Id == firstId), ct);
}
