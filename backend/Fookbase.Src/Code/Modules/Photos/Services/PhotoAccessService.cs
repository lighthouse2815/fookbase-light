using Fookbase.Api.Modules.Photos.Entities;
using Fookbase.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Photos.Services;

public sealed class PhotoAccessService(FookbaseDbContext db)
{
    public async Task<bool> CanViewAsync(PhotoAlbum album, Guid? viewerId, CancellationToken ct = default)
    {
        if (viewerId == album.OwnerUserId) return true;
        if (viewerId is null || await IsBlockedAsync(album.OwnerUserId, viewerId.Value, ct)) return false;
        return album.Privacy == PhotoAlbumPrivacy.Public ||
               album.Privacy == PhotoAlbumPrivacy.Friends && await AreFriendsAsync(album.OwnerUserId, viewerId.Value, ct);
    }

    public Task<bool> IsBlockedAsync(Guid firstId, Guid secondId, CancellationToken ct = default) =>
        db.BlockedUsers.AsNoTracking().AnyAsync(item =>
            (item.BlockerUserId == firstId && item.BlockedUserId == secondId) ||
            (item.BlockerUserId == secondId && item.BlockedUserId == firstId), ct);

    private Task<bool> AreFriendsAsync(Guid firstId, Guid secondId, CancellationToken ct) =>
        db.Friendships.AsNoTracking().AnyAsync(item =>
            (item.UserId1 == firstId && item.UserId2 == secondId) ||
            (item.UserId1 == secondId && item.UserId2 == firstId), ct);
}
