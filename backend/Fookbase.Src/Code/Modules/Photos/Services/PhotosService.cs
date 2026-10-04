using System.Text;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Photos.DTOs.Requests;
using Fookbase.Api.Modules.Photos.DTOs.Responses;
using Fookbase.Api.Modules.Photos.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Photos.Services;

public sealed class PhotosService(FookbaseDbContext db, PhotoAccessService access, MediaService media, TimeProvider time)
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;

    public async Task<ApplicationResult<PhotoAlbumResponse>> CreateAsync(Guid actorId, CreatePhotoAlbumRequest request, CancellationToken ct = default)
    {
        if (!TryPrivacy(request.Privacy, out var privacy)) return Bad<PhotoAlbumResponse>("invalid_album_privacy", "Album privacy is invalid.");
        try
        {
            var album = PhotoAlbum.CreateCustom(Guid.NewGuid(), actorId, request.Name, request.Description, privacy, time.GetUtcNow());
            db.PhotoAlbums.Add(album);
            await db.SaveChangesAsync(ct);
            return ApplicationResult<PhotoAlbumResponse>.Success(await ToAlbumAsync(album, actorId, ct));
        }
        catch (ArgumentException exception) { return Bad<PhotoAlbumResponse>("invalid_album", exception.Message); }
    }

    public async Task<ApplicationResult<PhotoAlbumResponse>> GetAsync(Guid albumId, Guid? viewerId, CancellationToken ct = default)
    {
        var album = await Active().SingleOrDefaultAsync(item => item.Id == albumId, ct);
        return album is null || !await access.CanViewAsync(album, viewerId, ct)
            ? NotFound<PhotoAlbumResponse>()
            : ApplicationResult<PhotoAlbumResponse>.Success(await ToAlbumAsync(album, viewerId, ct));
    }

    public async Task<ApplicationResult<PhotoAlbumResponse>> UpdateAsync(Guid actorId, Guid albumId, UpdatePhotoAlbumRequest request, CancellationToken ct = default)
    {
        if (!TryPrivacy(request.Privacy, out var privacy)) return Bad<PhotoAlbumResponse>("invalid_album_privacy", "Album privacy is invalid.");
        var album = await Active().SingleOrDefaultAsync(item => item.Id == albumId && item.OwnerUserId == actorId, ct);
        if (album is null) return NotFound<PhotoAlbumResponse>();
        try { album.UpdateCustom(request.Name, request.Description, privacy, time.GetUtcNow()); await db.SaveChangesAsync(ct); return ApplicationResult<PhotoAlbumResponse>.Success(await ToAlbumAsync(album, actorId, ct)); }
        catch (InvalidOperationException exception) { return Bad<PhotoAlbumResponse>("system_album_restricted", exception.Message, ApplicationErrorType.CONFLICT); }
        catch (ArgumentException exception) { return Bad<PhotoAlbumResponse>("invalid_album", exception.Message); }
    }

    public async Task<ApplicationResult> DeleteAsync(Guid actorId, Guid albumId, CancellationToken ct = default)
    {
        var album = await Active().SingleOrDefaultAsync(item => item.Id == albumId && item.OwnerUserId == actorId, ct);
        if (album is null) return NotFound();
        try { album.DeleteCustom(time.GetUtcNow()); await db.SaveChangesAsync(ct); return ApplicationResult.Success(); }
        catch (InvalidOperationException exception) { return Bad("system_album_restricted", exception.Message, ApplicationErrorType.CONFLICT); }
    }

    public async Task<ApplicationResult<PhotoCursorPageResponse<PhotoAlbumSummaryResponse>>> GetUserAlbumsAsync(Guid ownerId, Guid? viewerId, string? cursor, int limit, CancellationToken ct = default)
    {
        if (!ValidLimit(limit)) return Bad<PhotoCursorPageResponse<PhotoAlbumSummaryResponse>>("invalid_limit", "Limit must be between 1 and 100.");
        if (!await db.UserProfiles.AsNoTracking().AnyAsync(item => item.UserId == ownerId, ct)) return NotFound<PhotoCursorPageResponse<PhotoAlbumSummaryResponse>>();
        var source = Active().Where(item => item.OwnerUserId == ownerId);
        if (TryDecode(cursor, out var afterAt, out var afterId)) source = source.Where(item => item.CreatedAtUtc < afterAt || item.CreatedAtUtc == afterAt && item.Id.CompareTo(afterId) < 0);
        var all = await source.OrderByDescending(item => item.CreatedAtUtc).ThenByDescending(item => item.Id).Take(limit * 3 + 1).ToListAsync(ct);
        var allowed = new List<PhotoAlbum>();
        foreach (var album in all) if (await access.CanViewAsync(album, viewerId, ct)) allowed.Add(album);
        var page = allowed.Take(limit).ToList();
        var items = await Task.WhenAll(page.Select(item => ToSummaryAsync(item, ct)));
        return ApplicationResult<PhotoCursorPageResponse<PhotoAlbumSummaryResponse>>.Success(new(items, allowed.Count > limit ? Encode(page[^1].CreatedAtUtc, page[^1].Id) : null));
    }

    public async Task<ApplicationResult<PhotoCursorPageResponse<AlbumMediaResponse>>> GetMediaAsync(Guid albumId, Guid? viewerId, string? cursor, int limit, CancellationToken ct = default)
    {
        if (!ValidLimit(limit)) return Bad<PhotoCursorPageResponse<AlbumMediaResponse>>("invalid_limit", "Limit must be between 1 and 100.");
        var album = await Active().SingleOrDefaultAsync(item => item.Id == albumId, ct);
        if (album is null || !await access.CanViewAsync(album, viewerId, ct)) return NotFound<PhotoCursorPageResponse<AlbumMediaResponse>>();
        var query = from item in db.AlbumMedia.AsNoTracking()
                    join asset in db.MediaAssets.AsNoTracking() on item.MediaId equals asset.Id
                    where item.AlbumId == albumId && asset.MediaType == MediaType.IMAGE && asset.Status == MediaStatus.READY && asset.DeletedAtUtc == null
                    orderby item.SortOrder, item.MediaId
                    select item;
        if (TryDecode(cursor, out var sortAfter, out var mediaAfter)) query = query.Where(item => item.SortOrder > sortAfter.UtcTicks || item.SortOrder == sortAfter.UtcTicks && item.MediaId.CompareTo(mediaAfter) > 0);
        var rows = await query.Take(limit + 1).ToListAsync(ct);
        var visible = rows.Take(limit).Select(item => new AlbumMediaResponse(item.MediaId, item.Caption, item.SortOrder, item.AddedAtUtc, AccessPath(albumId, item.MediaId))).ToList();
        return ApplicationResult<PhotoCursorPageResponse<AlbumMediaResponse>>.Success(new(visible, rows.Count > limit ? Encode(new DateTimeOffset(rows[limit].SortOrder, TimeSpan.Zero), rows[limit].MediaId) : null));
    }

    public async Task<ApplicationResult<PhotoDetailResponse>> GetPhotoAsync(Guid albumId, Guid mediaId, Guid? viewerId, CancellationToken ct = default)
    {
        var album = await Active().SingleOrDefaultAsync(item => item.Id == albumId, ct);
        if (album is null || !await access.CanViewAsync(album, viewerId, ct)) return NotFound<PhotoDetailResponse>();
        var row = await db.AlbumMedia.AsNoTracking().SingleOrDefaultAsync(item => item.AlbumId == albumId && item.MediaId == mediaId, ct);
        if (row is null) return NotFound<PhotoDetailResponse>();
        var url = await media.CreateReadUrlAsync(mediaId, ct);
        return !url.Succeeded ? NotFound<PhotoDetailResponse>() : ApplicationResult<PhotoDetailResponse>.Success(new(mediaId, albumId, album.OwnerUserId, row.Caption, row.AddedAtUtc, url.Value!.Url));
    }

    public async Task<ApplicationResult<AlbumMediaResponse>> AddMediaAsync(Guid actorId, Guid albumId, Guid mediaId, CancellationToken ct = default)
    {
        var album = await Active().SingleOrDefaultAsync(item => item.Id == albumId && item.OwnerUserId == actorId, ct);
        if (album is null) return NotFound<AlbumMediaResponse>();
        if (album.AlbumType != PhotoAlbumType.CUSTOM) return Bad<AlbumMediaResponse>("system_album_restricted", "Photos cannot be added directly to a system album.", ApplicationErrorType.CONFLICT);
        var asset = await db.MediaAssets.AsNoTracking().SingleOrDefaultAsync(item => item.Id == mediaId, ct);
        if (asset is null || asset.OwnerUserId != actorId) return Bad<AlbumMediaResponse>("media_not_owned", "Only your image can be added to an album.", ApplicationErrorType.FORBIDDEN);
        if (asset.MediaType != MediaType.IMAGE || asset.Status != MediaStatus.READY || asset.DeletedAtUtc is not null) return Bad<AlbumMediaResponse>("invalid_media", "Only ready images can be added to an album.", ApplicationErrorType.CONFLICT);
        if (await db.AlbumMedia.AnyAsync(item => item.AlbumId == albumId && item.MediaId == mediaId, ct)) return Bad<AlbumMediaResponse>("album_media_exists", "This image is already in the album.", ApplicationErrorType.CONFLICT);
        var anotherCustom = await (from item in db.AlbumMedia
                                   join candidate in Active() on item.AlbumId equals candidate.Id
                                   where item.MediaId == mediaId && candidate.AlbumType == PhotoAlbumType.CUSTOM
                                   select item).AnyAsync(ct);
        if (anotherCustom) return Bad<AlbumMediaResponse>("custom_album_membership_exists", "An image can belong to only one custom album.", ApplicationErrorType.CONFLICT);
        var max = await db.AlbumMedia.Where(item => item.AlbumId == albumId).Select(item => (long?)item.SortOrder).MaxAsync(ct) ?? -1;
        var row = AlbumMedia.Create(albumId, mediaId, max + 1, time.GetUtcNow());
        db.AlbumMedia.Add(row);
        await db.SaveChangesAsync(ct);
        return ApplicationResult<AlbumMediaResponse>.Success(new(mediaId, null, row.SortOrder, row.AddedAtUtc, AccessPath(albumId, mediaId)));
    }

    public async Task<ApplicationResult<AlbumMediaResponse>> UpdateCaptionAsync(Guid actorId, Guid albumId, Guid mediaId, UpdateAlbumMediaRequest request, CancellationToken ct = default)
    {
        var album = await Active().SingleOrDefaultAsync(item => item.Id == albumId && item.OwnerUserId == actorId, ct);
        if (album is null) return NotFound<AlbumMediaResponse>();
        var row = await db.AlbumMedia.SingleOrDefaultAsync(item => item.AlbumId == albumId && item.MediaId == mediaId, ct);
        if (row is null) return NotFound<AlbumMediaResponse>();
        try { row.UpdateCaption(request.Caption); await db.SaveChangesAsync(ct); return ApplicationResult<AlbumMediaResponse>.Success(new(mediaId, row.Caption, row.SortOrder, row.AddedAtUtc, AccessPath(albumId, mediaId))); }
        catch (ArgumentException exception) { return Bad<AlbumMediaResponse>("invalid_caption", exception.Message); }
    }

    public async Task<ApplicationResult> RemoveMediaAsync(Guid actorId, Guid albumId, Guid mediaId, CancellationToken ct = default)
    {
        var album = await Active().SingleOrDefaultAsync(item => item.Id == albumId && item.OwnerUserId == actorId, ct);
        if (album is null) return NotFound();
        var row = await db.AlbumMedia.SingleOrDefaultAsync(item => item.AlbumId == albumId && item.MediaId == mediaId, ct);
        if (row is null) return NotFound();
        var active = await db.UserProfiles.AsNoTracking().AnyAsync(item => item.UserId == actorId && (item.AvatarMediaId == mediaId || item.CoverMediaId == mediaId), ct);
        if (active) return Bad("active_profile_media", "Change the active avatar or cover before removing it from album history.", ApplicationErrorType.CONFLICT);
        db.AlbumMedia.Remove(row);
        await db.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }

    public async Task AddSystemMediaAsync(Guid ownerId, PhotoAlbumType type, Guid mediaId, CancellationToken ct = default)
    {
        if (type == PhotoAlbumType.CUSTOM) throw new ArgumentException("System album type is required.", nameof(type));
        var asset = await db.MediaAssets.AsNoTracking().SingleOrDefaultAsync(item => item.Id == mediaId, ct);
        if (asset is null || asset.OwnerUserId != ownerId || asset.MediaType != MediaType.IMAGE || asset.Status != MediaStatus.READY || asset.DeletedAtUtc is not null) return;
        var album = await GetOrCreateSystemAlbumAsync(ownerId, type, ct);
        if (await db.AlbumMedia.AnyAsync(item => item.AlbumId == album.Id && item.MediaId == mediaId, ct)) return;
        var max = await db.AlbumMedia.Where(item => item.AlbumId == album.Id).Select(item => (long?)item.SortOrder).MaxAsync(ct) ?? -1;
        db.AlbumMedia.Add(AlbumMedia.Create(album.Id, mediaId, max + 1, time.GetUtcNow()));
        await db.SaveChangesAsync(ct);
    }

    private async Task<PhotoAlbum> GetOrCreateSystemAlbumAsync(Guid ownerId, PhotoAlbumType type, CancellationToken ct)
    {
        var album = await Active().SingleOrDefaultAsync(item => item.OwnerUserId == ownerId && item.AlbumType == type, ct);
        if (album is not null) return album;
        album = PhotoAlbum.CreateSystem(Guid.NewGuid(), ownerId, type, time.GetUtcNow());
        db.PhotoAlbums.Add(album);
        try { await db.SaveChangesAsync(ct); return album; }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return await Active().SingleAsync(item => item.OwnerUserId == ownerId && item.AlbumType == type, ct);
        }
    }

    private IQueryable<PhotoAlbum> Active() => db.PhotoAlbums.Where(item => item.DeletedAtUtc == null);
    private async Task<PhotoAlbumResponse> ToAlbumAsync(PhotoAlbum album, Guid? viewerId, CancellationToken ct) { var summary = await ToSummaryAsync(album, ct); return new(album.Id, album.OwnerUserId, album.Name, album.Description, summary.AlbumType, summary.Privacy, summary.PhotoCount, summary.PreviewUrl, album.CreatedAtUtc, album.UpdatedAtUtc, viewerId == album.OwnerUserId && album.AlbumType == PhotoAlbumType.CUSTOM); }
    private async Task<PhotoAlbumSummaryResponse> ToSummaryAsync(PhotoAlbum album, CancellationToken ct) { var rows = db.AlbumMedia.AsNoTracking().Where(item => item.AlbumId == album.Id); var count = await rows.CountAsync(ct); var preview = await rows.OrderBy(item => item.SortOrder).ThenBy(item => item.MediaId).Select(item => (Guid?)item.MediaId).FirstOrDefaultAsync(ct); return new(album.Id, album.Name, album.AlbumType.ToApiName().ToLowerInvariant(), album.Privacy.ToApiName().ToLowerInvariant(), count, preview is null ? null : AccessPath(album.Id, preview.Value), album.CreatedAtUtc); }
    private static string AccessPath(Guid albumId, Guid mediaId) => $"/api/albums/{albumId}/media/{mediaId}/access";
    private static bool TryPrivacy(string? value, out PhotoAlbumPrivacy privacy) => EnumText.TryParse(value, true, out privacy) && Enum.IsDefined(privacy);
    private static bool ValidLimit(int value) => value is >= 1 and <= MaximumPageSize;
    private static string Encode(DateTimeOffset value, Guid id) => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{value.UtcTicks}|{id}"));
    private static bool TryDecode(string? value, out DateTimeOffset at, out Guid id) { at=default;id=default; try { var parts=Encoding.UTF8.GetString(Convert.FromBase64String(value??string.Empty)).Split('|'); return parts.Length==2 && long.TryParse(parts[0],out var ticks) && Guid.TryParse(parts[1],out id) && (at=new DateTimeOffset(ticks,TimeSpan.Zero))!=default; } catch { return false; } }
    private static ApplicationResult<T> Bad<T>(string code, string message, ApplicationErrorType type = ApplicationErrorType.VALIDATION) => ApplicationResult<T>.Failure(new(code, message, type));
    private static ApplicationResult Bad(string code = "album_not_found", string message = "The album was not found.", ApplicationErrorType type = ApplicationErrorType.NOT_FOUND) => ApplicationResult.Failure(new(code, message, type));
    private static ApplicationResult<T> NotFound<T>() => Bad<T>("album_not_found", "The album was not found.", ApplicationErrorType.NOT_FOUND);
    private static ApplicationResult NotFound() => Bad();
}
