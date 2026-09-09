using Fookbase.Api.Modules.Media.Config;
using Fookbase.Api.Modules.Media.DTOs;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Media.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Media.Services;

public sealed class MediaService(
    MediaDbContext dbContext,
    IObjectStorage objectStorage,
    MediaOptions options,
    TimeProvider timeProvider)
{
    private sealed record SupportedFormat(MediaType MediaType, string Extension);

    private static readonly IReadOnlyDictionary<string, SupportedFormat> SupportedFormats =
        new Dictionary<string, SupportedFormat>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = new(MediaType.Image, ".jpg"),
            ["image/png"] = new(MediaType.Image, ".png"),
            ["image/webp"] = new(MediaType.Image, ".webp"),
            ["video/mp4"] = new(MediaType.Video, ".mp4"),
            ["video/webm"] = new(MediaType.Video, ".webm")
        };

    public async Task<ApplicationResult<UploadIntentResponse>> CreateUploadAsync(
        Guid ownerUserId,
        CreateUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var fileName = Path.GetFileName(request.FileName ?? string.Empty);
        var contentType = request.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255)
        {
            return Failure<UploadIntentResponse>("invalid_file_name",
                "A file name containing at most 255 characters is required.");
        }

        if (!SupportedFormats.TryGetValue(contentType, out var format))
        {
            return Failure<UploadIntentResponse>("unsupported_media_type",
                "Supported media types are JPEG, PNG, WebP, MP4 and WebM.");
        }

        var maximumSize = format.MediaType == MediaType.Image
            ? options.MaximumImageSizeBytes
            : options.MaximumVideoSizeBytes;
        if (request.SizeBytes <= 0 || request.SizeBytes > maximumSize)
        {
            return Failure<UploadIntentResponse>("invalid_file_size",
                $"The declared {format.MediaType.ToString().ToLowerInvariant()} size is invalid.");
        }

        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(options.UploadUrlExpiryMinutes);
        var id = Guid.NewGuid();
        var objectKey = $"{ownerUserId:N}/{id:N}{format.Extension}";
        var asset = MediaAsset.CreatePending(
            id, ownerUserId, format.MediaType, objectKey, fileName, contentType,
            request.SizeBytes, now, expiresAt);
        dbContext.MediaAssets.Add(asset);
        await dbContext.SaveChangesAsync(cancellationToken);

        var uploadUrl = await objectStorage.CreatePresignedPutUrlAsync(
            objectKey, TimeSpan.FromMinutes(options.UploadUrlExpiryMinutes), cancellationToken);
        return ApplicationResult<UploadIntentResponse>.Success(
            new UploadIntentResponse(id, uploadUrl, expiresAt));
    }

    public async Task<ApplicationResult<MediaResponse>> CompleteAsync(
        Guid ownerUserId,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        var asset = await dbContext.MediaAssets.SingleOrDefaultAsync(
            item => item.Id == mediaId,
            cancellationToken);
        var accessError = CheckOwner(asset, ownerUserId);
        if (accessError is not null)
        {
            return ApplicationResult<MediaResponse>.Failure(accessError);
        }

        if (asset!.Status == MediaStatus.Ready)
        {
            return ApplicationResult<MediaResponse>.Success(ToResponse(asset));
        }

        if (asset.Status != MediaStatus.PendingUpload)
        {
            return Conflict<MediaResponse>("invalid_media_status", "The upload cannot be completed.");
        }

        var now = timeProvider.GetUtcNow();
        if (asset.UploadExpiresAtUtc <= now)
        {
            asset.MarkFailed();
            await SaveFailedAsync(asset, cancellationToken);
            return Conflict<MediaResponse>("upload_expired", "The upload intent has expired.");
        }

        var storedObject = await objectStorage.GetInfoAsync(asset.ObjectKey, cancellationToken);
        if (storedObject is null)
        {
            return Conflict<MediaResponse>("upload_object_missing", "The uploaded object was not found.");
        }

        var maximumSize = asset.MediaType == MediaType.Image
            ? options.MaximumImageSizeBytes
            : options.MaximumVideoSizeBytes;
        if (storedObject.SizeBytes != asset.DeclaredSizeBytes || storedObject.SizeBytes > maximumSize)
        {
            asset.MarkFailed();
            await SaveFailedAsync(asset, cancellationToken);
            return Failure<MediaResponse>("uploaded_size_mismatch",
                "The uploaded object size does not match the declared size.");
        }

        var prefixLength = checked((int)Math.Min(32L, storedObject.SizeBytes));
        var prefix = await objectStorage.ReadPrefixAsync(asset.ObjectKey, prefixLength, cancellationToken);
        var detectedContentType = DetectContentType(prefix);
        if (!string.Equals(detectedContentType, asset.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            asset.MarkFailed();
            await SaveFailedAsync(asset, cancellationToken);
            return Failure<MediaResponse>("invalid_file_signature",
                "The uploaded object signature does not match its declared content type.");
        }

        asset.MarkReady(storedObject.SizeBytes, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult<MediaResponse>.Success(ToResponse(asset));
    }

    public async Task<ApplicationResult<MediaResponse>> GetMetadataAsync(
        Guid ownerUserId,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        var asset = await dbContext.MediaAssets.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == mediaId,
            cancellationToken);
        var accessError = CheckOwner(asset, ownerUserId);
        return accessError is null
            ? ApplicationResult<MediaResponse>.Success(ToResponse(asset!))
            : ApplicationResult<MediaResponse>.Failure(accessError);
    }

    public async Task<ApplicationResult<MediaReadUrlResponse>> CreateReadUrlAsync(
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        var asset = await dbContext.MediaAssets.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == mediaId,
            cancellationToken);
        if (asset is null || asset.Status != MediaStatus.Ready || asset.DeletedAtUtc is not null)
        {
            return ApplicationResult<MediaReadUrlResponse>.Failure(NotFound());
        }

        var expiry = TimeSpan.FromMinutes(options.DownloadUrlExpiryMinutes);
        var url = await objectStorage.CreatePresignedGetUrlAsync(asset.ObjectKey, expiry, cancellationToken);
        return ApplicationResult<MediaReadUrlResponse>.Success(
            new MediaReadUrlResponse(asset.Id, url, timeProvider.GetUtcNow().Add(expiry)));
    }

    public async Task<ApplicationResult> ValidatePostMediaAsync(
        Guid ownerUserId,
        IReadOnlyCollection<Guid> mediaIds,
        CancellationToken cancellationToken = default)
    {
        foreach (var mediaId in mediaIds)
        {
            var asset = await dbContext.MediaAssets.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == mediaId,
                cancellationToken);
            if (asset is null || asset.Status != MediaStatus.Ready || asset.DeletedAtUtc is not null)
            {
                return ApplicationResult.Failure(new ApplicationError(
                    "invalid_media", "Every attachment must be ready.", ApplicationErrorType.Conflict));
            }

            if (asset.OwnerUserId != ownerUserId)
            {
                return ApplicationResult.Failure(new ApplicationError(
                    "media_not_owned", "Only the media owner can attach it.", ApplicationErrorType.Forbidden));
            }
        }

        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult> SynchronizePostReferencesAsync(
        Guid ownerUserId,
        Guid postId,
        IReadOnlyCollection<Guid> mediaIds,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidatePostMediaAsync(ownerUserId, mediaIds, cancellationToken);
        if (!validation.Succeeded)
        {
            return validation;
        }

        var currentReferences = await dbContext.MediaReferences
            .Where(reference => reference.PostId == postId)
            .ToListAsync(cancellationToken);
        var desiredMediaIds = mediaIds.ToHashSet();
        dbContext.MediaReferences.RemoveRange(
            currentReferences.Where(reference => !desiredMediaIds.Contains(reference.MediaId)));
        foreach (var mediaId in desiredMediaIds.Except(currentReferences.Select(reference => reference.MediaId)))
        {
            dbContext.MediaReferences.Add(MediaReference.Create(mediaId, postId, timeProvider.GetUtcNow()));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult.Success();
    }

    public Task RemovePostReferencesAsync(Guid postId, CancellationToken cancellationToken = default) =>
        dbContext.MediaReferences
            .Where(reference => reference.PostId == postId)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task<ApplicationResult> DeleteAsync(
        Guid ownerUserId,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        var asset = await dbContext.MediaAssets.SingleOrDefaultAsync(
            item => item.Id == mediaId,
            cancellationToken);
        var accessError = CheckOwner(asset, ownerUserId);
        if (accessError is not null)
        {
            return ApplicationResult.Failure(accessError);
        }

        if (asset!.Status == MediaStatus.Deleted)
        {
            return ApplicationResult.Success();
        }

        if (asset.Status != MediaStatus.Ready)
        {
            return ApplicationResult.Failure(new ApplicationError(
                "invalid_media_status", "Only ready media can be deleted.", ApplicationErrorType.Conflict));
        }

        if (await dbContext.MediaReferences.AnyAsync(reference => reference.MediaId == mediaId, cancellationToken))
        {
            return ApplicationResult.Failure(new ApplicationError(
                "media_is_referenced", "Attached media cannot be deleted.", ApplicationErrorType.Conflict));
        }

        var now = timeProvider.GetUtcNow();
        asset.Delete(now);
        dbContext.ObjectDeletions.Add(ObjectDeletion.Create(asset.Id, asset.ObjectKey, now));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult.Success();
    }

    private static ApplicationError? CheckOwner(MediaAsset? asset, Guid ownerUserId)
    {
        if (asset is null)
        {
            return NotFound();
        }

        return asset.OwnerUserId == ownerUserId
            ? null
            : new ApplicationError("media_forbidden", "Only the media owner may access this metadata.",
                ApplicationErrorType.Forbidden);
    }

    private async Task SaveFailedAsync(MediaAsset asset, CancellationToken cancellationToken)
    {
        dbContext.ObjectDeletions.Add(ObjectDeletion.Create(
            asset.Id,
            asset.ObjectKey,
            timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? DetectContentType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff)
            return "image/jpeg";
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }))
            return "image/png";
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
            return "image/webp";
        if (bytes.Length >= 12 && bytes[4..8].SequenceEqual("ftyp"u8))
            return "video/mp4";
        if (bytes.Length >= 4 && bytes[..4].SequenceEqual(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 }))
            return "video/webm";
        return null;
    }

    private static MediaResponse ToResponse(MediaAsset asset) => new(
        asset.Id, asset.OwnerUserId,
        asset.MediaType.ToString().ToLowerInvariant(),
        asset.Status.ToString(), asset.OriginalFileName, asset.ContentType,
        asset.DeclaredSizeBytes, asset.ActualSizeBytes, asset.CreatedAtUtc,
        asset.UploadExpiresAtUtc, asset.UploadedAtUtc, asset.DeletedAtUtc);

    private static ApplicationError NotFound() =>
        new("media_not_found", "The media asset was not found.", ApplicationErrorType.NotFound);

    private static ApplicationResult<T> Failure<T>(string code, string message) =>
        ApplicationResult<T>.Failure(new ApplicationError(code, message, ApplicationErrorType.Validation));

    private static ApplicationResult<T> Conflict<T>(string code, string message) =>
        ApplicationResult<T>.Failure(new ApplicationError(code, message, ApplicationErrorType.Conflict));
}
