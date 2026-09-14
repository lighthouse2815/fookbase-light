using Fookbase.Api.Modules.Media.Config;
using Fookbase.Api.Modules.Groups.Entities;
using Fookbase.Api.Modules.Media.Common;
using Fookbase.Api.Modules.Media.DTOs.Requests;
using Fookbase.Api.Modules.Media.DTOs.Responses;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Messages.Entities;
using Fookbase.Api.Modules.Pages.Entities;
using Fookbase.Api.Modules.Stories.Entities;
using Fookbase.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Media.Services;

public sealed class MediaService(
    FookbaseDbContext dbContext,
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

        var uploadIntent = await objectStorage.CreateDirectUploadIntentAsync(
            objectKey, format.MediaType, TimeSpan.FromMinutes(options.UploadUrlExpiryMinutes), cancellationToken);
        return ApplicationResult<UploadIntentResponse>.Success(
            new UploadIntentResponse(id, uploadIntent.UploadUrl, "POST", uploadIntent.UploadParameters, expiresAt));
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

        if (asset.Status == MediaStatus.Processing)
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

        var storedObject = await objectStorage.GetInfoAsync(asset.ObjectKey, asset.MediaType, cancellationToken);
        if (storedObject is null)
        {
            return Conflict<MediaResponse>("upload_object_missing", "The uploaded object was not found.");
        }

        var maximumSize = asset.MediaType == MediaType.Image
            ? options.MaximumImageSizeBytes
            : options.MaximumVideoSizeBytes;
        if (storedObject.MediaType != asset.MediaType || !storedObject.IsAuthenticated ||
            storedObject.SizeBytes != asset.DeclaredSizeBytes || storedObject.SizeBytes > maximumSize)
        {
            asset.MarkFailed();
            await SaveFailedAsync(asset, cancellationToken);
            return Failure<MediaResponse>("uploaded_size_mismatch",
                "The uploaded object size does not match the declared size.");
        }

        var prefixLength = checked((int)Math.Min(32L, storedObject.SizeBytes));
        var prefix = await objectStorage.ReadPrefixAsync(asset.ObjectKey, asset.MediaType, prefixLength, cancellationToken);
        var detectedContentType = DetectContentType(prefix);
        if (!string.Equals(detectedContentType, asset.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            asset.MarkFailed();
            await SaveFailedAsync(asset, cancellationToken);
            return Failure<MediaResponse>("invalid_file_signature",
                "The uploaded object signature does not match its declared content type.");
        }

        if (asset.MediaType == MediaType.Image)
        {
            asset.MarkReady(storedObject.SizeBytes, now);
        }
        else
        {
            asset.MarkProcessing(storedObject.SizeBytes, now);
            dbContext.MediaProcessingJobs.Add(MediaProcessingJob.Create(asset.Id, now));
        }
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
        var objectKey = asset.MediaType == MediaType.Video
            ? asset.ProcessedObjectKey
            : asset.ObjectKey;
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return ApplicationResult<MediaReadUrlResponse>.Failure(NotFound());
        }

        var url = await objectStorage.CreateSignedGetUrlAsync(objectKey, asset.MediaType, cancellationToken);
        return ApplicationResult<MediaReadUrlResponse>.Success(
            new MediaReadUrlResponse(
                asset.Id,
                url,
                timeProvider.GetUtcNow().Add(expiry),
                asset.MediaType.ToString().ToLowerInvariant(),
                asset.MediaType == MediaType.Video ? "video/mp4" : asset.ContentType));
    }

    public async Task<ApplicationResult<MediaReadUrlResponse>> CreatePosterReadUrlAsync(
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        var asset = await dbContext.MediaAssets.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == mediaId,
            cancellationToken);
        if (asset is null || asset.Status != MediaStatus.Ready || asset.DeletedAtUtc is not null ||
            asset.MediaType != MediaType.Video || string.IsNullOrWhiteSpace(asset.PosterObjectKey))
        {
            return ApplicationResult<MediaReadUrlResponse>.Failure(NotFound());
        }

        var expiry = TimeSpan.FromMinutes(options.DownloadUrlExpiryMinutes);
        var url = await objectStorage.CreateSignedGetUrlAsync(
            asset.PosterObjectKey, MediaType.Image, cancellationToken);
        return ApplicationResult<MediaReadUrlResponse>.Success(
            new MediaReadUrlResponse(
                asset.Id,
                url,
                timeProvider.GetUtcNow().Add(expiry),
                "image",
                "image/jpeg"));
    }

    public async Task<ApplicationResult<MediaReadUrlResponse>> CreateOwnerReadUrlAsync(
        Guid ownerUserId,
        Guid mediaId,
        bool poster,
        CancellationToken cancellationToken = default)
    {
        var asset = await dbContext.MediaAssets.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == mediaId,
            cancellationToken);
        var accessError = CheckOwner(asset, ownerUserId);
        if (accessError is not null)
        {
            return ApplicationResult<MediaReadUrlResponse>.Failure(accessError);
        }

        return poster
            ? await CreatePosterReadUrlAsync(mediaId, cancellationToken)
            : await CreateReadUrlAsync(mediaId, cancellationToken);
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

    public async Task<ApplicationResult> ValidateReelVideoAsync(
        Guid ownerUserId,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        var asset = await dbContext.MediaAssets.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == mediaId,
            cancellationToken);
        if (asset is null || asset.Status != MediaStatus.Ready || asset.DeletedAtUtc is not null ||
            asset.MediaType != MediaType.Video || string.IsNullOrWhiteSpace(asset.ProcessedObjectKey) ||
            string.IsNullOrWhiteSpace(asset.PosterObjectKey) || asset.DurationMs is null ||
            asset.Width is null || asset.Height is null)
        {
            return ApplicationResult.Failure(new ApplicationError(
                "invalid_reel_video",
                "The reel video must be fully processed and ready.",
                ApplicationErrorType.Conflict));
        }

        if (asset.OwnerUserId != ownerUserId)
        {
            return ApplicationResult.Failure(new ApplicationError(
                "media_not_owned", "Only the media owner can publish this reel video.",
                ApplicationErrorType.Forbidden));
        }

        return asset.DurationMs < options.MinimumReelDurationMs ||
               asset.DurationMs > options.MaximumReelDurationMs
            ? ApplicationResult.Failure(new ApplicationError(
                "invalid_reel_duration",
                $"Reel videos must be between {options.MinimumReelDurationMs} and {options.MaximumReelDurationMs} milliseconds.",
                ApplicationErrorType.Validation))
            : ApplicationResult.Success();
    }

    public async Task<ApplicationResult> ValidateStoryMediaAsync(
        Guid ownerUserId,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        var asset = await dbContext.MediaAssets.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == mediaId,
            cancellationToken);
        if (asset is null || asset.Status != MediaStatus.Ready || asset.DeletedAtUtc is not null)
        {
            return ApplicationResult.Failure(new ApplicationError(
                "invalid_story_media", "Story media must be ready.", ApplicationErrorType.Conflict));
        }

        if (asset.OwnerUserId != ownerUserId)
        {
            return ApplicationResult.Failure(new ApplicationError(
                "media_not_owned", "Only the media owner can publish it as a story.",
                ApplicationErrorType.Forbidden));
        }

        if (asset.MediaType == MediaType.Image)
        {
            return ApplicationResult.Success();
        }

        if (asset.MediaType != MediaType.Video ||
            string.IsNullOrWhiteSpace(asset.ProcessedObjectKey) ||
            string.IsNullOrWhiteSpace(asset.PosterObjectKey) ||
            asset.DurationMs is null || asset.Width is null || asset.Height is null)
        {
            return ApplicationResult.Failure(new ApplicationError(
                "invalid_story_video", "Story video must be fully processed and ready.",
                ApplicationErrorType.Conflict));
        }

        return asset.DurationMs > options.MaximumStoryVideoDurationMs
            ? ApplicationResult.Failure(new ApplicationError(
                "invalid_story_duration",
                $"Story videos cannot exceed {options.MaximumStoryVideoDurationMs} milliseconds.",
                ApplicationErrorType.Validation))
            : ApplicationResult.Success();
    }

    public async Task<ApplicationResult> ValidateProfileImageAsync(
        Guid ownerUserId,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        var asset = await dbContext.MediaAssets.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == mediaId,
            cancellationToken);
        if (asset is null || asset.Status != MediaStatus.Ready || asset.DeletedAtUtc is not null)
        {
            return ApplicationResult.Failure(new ApplicationError(
                "invalid_media", "The profile image must be ready.", ApplicationErrorType.Validation));
        }

        if (asset.OwnerUserId != ownerUserId)
        {
            return ApplicationResult.Failure(new ApplicationError(
                "media_not_owned", "Only the media owner can use this profile image.", ApplicationErrorType.Forbidden));
        }

        return asset.MediaType == MediaType.Image
            ? ApplicationResult.Success()
            : ApplicationResult.Failure(new ApplicationError(
                "invalid_profile_media_type", "Profile media must be an image.", ApplicationErrorType.Validation));
    }

    public async Task<ApplicationResult> ValidateGroupCoverImageAsync(
        Guid ownerUserId,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        var asset = await dbContext.MediaAssets.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == mediaId,
            cancellationToken);
        if (asset is null || asset.Status != MediaStatus.Ready || asset.DeletedAtUtc is not null)
        {
            return ApplicationResult.Failure(new ApplicationError(
                "invalid_media", "The group cover image must be ready.", ApplicationErrorType.Validation));
        }

        if (asset.OwnerUserId != ownerUserId)
        {
            return ApplicationResult.Failure(new ApplicationError(
                "media_not_owned", "Only the media owner can use this group cover image.",
                ApplicationErrorType.Forbidden));
        }

        return asset.MediaType == MediaType.Image
            ? ApplicationResult.Success()
            : ApplicationResult.Failure(new ApplicationError(
                "invalid_group_cover_media_type", "Group cover media must be an image.",
                ApplicationErrorType.Validation));
    }

    public async Task<ApplicationResult> ValidatePageImageAsync(
        Guid ownerUserId,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        var asset = await dbContext.MediaAssets.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == mediaId,
            cancellationToken);
        if (asset is null || asset.Status != MediaStatus.Ready || asset.DeletedAtUtc is not null)
        {
            return ApplicationResult.Failure(new ApplicationError(
                "invalid_media", "The Page image must be ready.", ApplicationErrorType.Validation));
        }

        if (asset.OwnerUserId != ownerUserId)
        {
            return ApplicationResult.Failure(new ApplicationError(
                "media_not_owned", "Only the media owner can use this Page image.", ApplicationErrorType.Forbidden));
        }

        return asset.MediaType == MediaType.Image
            ? ApplicationResult.Success()
            : ApplicationResult.Failure(new ApplicationError(
                "invalid_page_media_type", "Page avatar and cover media must be images.", ApplicationErrorType.Validation));
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

    public async Task SynchronizeProfileReferencesAsync(
        Guid userId,
        Guid? avatarMediaId,
        Guid? coverMediaId,
        CancellationToken cancellationToken = default)
    {
        var desiredMediaIds = new Dictionary<ProfileMediaSlot, Guid>();
        if (avatarMediaId is not null)
        {
            desiredMediaIds[ProfileMediaSlot.Avatar] = avatarMediaId.Value;
        }

        if (coverMediaId is not null)
        {
            desiredMediaIds[ProfileMediaSlot.Cover] = coverMediaId.Value;
        }

        var currentReferences = await dbContext.ProfileMediaReferences
            .Where(reference => reference.UserId == userId)
            .ToListAsync(cancellationToken);
        dbContext.ProfileMediaReferences.RemoveRange(currentReferences.Where(reference =>
            !desiredMediaIds.TryGetValue(reference.Slot, out var desiredMediaId) ||
            desiredMediaId != reference.MediaId));
        foreach (var (slot, mediaId) in desiredMediaIds)
        {
            if (currentReferences.Any(reference =>
                    reference.Slot == slot && reference.MediaId == mediaId))
            {
                continue;
            }

            dbContext.ProfileMediaReferences.Add(ProfileMediaReference.Create(
                userId,
                slot,
                mediaId,
                timeProvider.GetUtcNow()));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task RemovePostReferencesAsync(Guid postId, CancellationToken cancellationToken = default) =>
        dbContext.MediaReferences
            .Where(reference => reference.PostId == postId)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task SynchronizeGroupCoverReferenceAsync(
        Guid groupId,
        Guid? mediaId,
        CancellationToken cancellationToken = default)
    {
        var currentReference = await dbContext.GroupCoverMediaReferences.SingleOrDefaultAsync(
            reference => reference.GroupId == groupId,
            cancellationToken);
        if (mediaId is null)
        {
            if (currentReference is not null)
            {
                dbContext.GroupCoverMediaReferences.Remove(currentReference);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        if (currentReference is null)
        {
            dbContext.GroupCoverMediaReferences.Add(GroupCoverMediaReference.Create(
                groupId,
                mediaId.Value,
                timeProvider.GetUtcNow()));
        }
        else if (currentReference.MediaId != mediaId.Value)
        {
            dbContext.GroupCoverMediaReferences.Remove(currentReference);
            dbContext.GroupCoverMediaReferences.Add(GroupCoverMediaReference.Create(
                groupId,
                mediaId.Value,
                timeProvider.GetUtcNow()));
        }
        else
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SynchronizePageReferencesAsync(
        Guid pageId,
        Guid? avatarMediaId,
        Guid? coverMediaId,
        CancellationToken cancellationToken = default)
    {
        var desiredMediaIds = new Dictionary<PageMediaSlot, Guid>();
        if (avatarMediaId is not null)
        {
            desiredMediaIds[PageMediaSlot.Avatar] = avatarMediaId.Value;
        }

        if (coverMediaId is not null)
        {
            desiredMediaIds[PageMediaSlot.Cover] = coverMediaId.Value;
        }

        var currentReferences = await dbContext.PageMediaReferences
            .Where(reference => reference.PageId == pageId)
            .ToListAsync(cancellationToken);
        dbContext.PageMediaReferences.RemoveRange(currentReferences.Where(reference =>
            !desiredMediaIds.TryGetValue(reference.Slot, out var desiredMediaId) || desiredMediaId != reference.MediaId));
        foreach (var (slot, mediaId) in desiredMediaIds)
        {
            if (currentReferences.Any(reference => reference.Slot == slot && reference.MediaId == mediaId))
            {
                continue;
            }

            dbContext.PageMediaReferences.Add(PageMediaReference.Create(pageId, slot, mediaId, timeProvider.GetUtcNow()));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

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

        var isReferencedByPost = await dbContext.MediaReferences
            .AnyAsync(reference => reference.MediaId == mediaId, cancellationToken);
        var isReferencedByProfile = await dbContext.ProfileMediaReferences
            .AnyAsync(reference => reference.MediaId == mediaId, cancellationToken);
        var isReferencedByActiveProfile = await dbContext.UserProfiles.AsNoTracking()
            .AnyAsync(profile =>
                profile.AvatarMediaId == mediaId || profile.CoverMediaId == mediaId,
                cancellationToken);
        var isReferencedByGroupCover = await dbContext.GroupCoverMediaReferences
            .AnyAsync(reference => reference.MediaId == mediaId, cancellationToken);
        var isReferencedByActiveGroup = await dbContext.Groups.AsNoTracking()
            .AnyAsync(group => group.CoverMediaId == mediaId && group.DeletedAtUtc == null, cancellationToken);
        var isReferencedByPage = await dbContext.PageMediaReferences
            .AnyAsync(reference => reference.MediaId == mediaId, cancellationToken);
        var isReferencedByActivePage = await dbContext.Pages.AsNoTracking()
            .AnyAsync(page => (page.AvatarMediaId == mediaId || page.CoverMediaId == mediaId) && page.DeletedAtUtc == null,
                cancellationToken);
        var isReferencedByMessage = await dbContext.MessageAttachments.AsNoTracking()
            .AnyAsync(reference => reference.MediaId == mediaId, cancellationToken);
        var isReferencedByConversationPhoto = await dbContext.Conversations.AsNoTracking()
            .AnyAsync(conversation => conversation.PhotoMediaId == mediaId, cancellationToken);
        var isReferencedByStory = await dbContext.StoryMediaReferences.AsNoTracking()
            .AnyAsync(reference => reference.MediaId == mediaId, cancellationToken);
        var isReferencedByEvent = await dbContext.EventCoverMediaReferences.AsNoTracking()
            .AnyAsync(reference => reference.MediaId == mediaId, cancellationToken);
        var isReferencedByAlbum = await dbContext.AlbumMedia.AsNoTracking()
            .AnyAsync(reference => reference.MediaId == mediaId, cancellationToken);
        if (isReferencedByPost ||
            isReferencedByProfile ||
            isReferencedByActiveProfile ||
            isReferencedByGroupCover ||
            isReferencedByActiveGroup ||
            isReferencedByPage ||
            isReferencedByActivePage ||
            isReferencedByMessage ||
            isReferencedByConversationPhoto ||
            isReferencedByStory ||
            isReferencedByEvent ||
            isReferencedByAlbum)
        {
            return ApplicationResult.Failure(new ApplicationError(
                "media_is_referenced", "Attached media cannot be deleted.", ApplicationErrorType.Conflict));
        }

        var now = timeProvider.GetUtcNow();
        asset.Delete(now);
        dbContext.ObjectDeletions.Add(ObjectDeletion.Create(asset.Id, asset.ObjectKey, now));
        if (!string.IsNullOrWhiteSpace(asset.ProcessedObjectKey))
        {
            dbContext.ObjectDeletions.Add(ObjectDeletion.Create(asset.Id, asset.ProcessedObjectKey, now));
        }

        if (!string.IsNullOrWhiteSpace(asset.PosterObjectKey))
        {
            dbContext.ObjectDeletions.Add(ObjectDeletion.Create(asset.Id, asset.PosterObjectKey, now));
        }
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
        asset.UploadExpiresAtUtc, asset.UploadedAtUtc, asset.DeletedAtUtc,
        asset.DurationMs, asset.Width, asset.Height,
        asset.MediaType == MediaType.Video && asset.Status == MediaStatus.Ready,
        asset.ProcessedAtUtc);

    private static ApplicationError NotFound() =>
        new("media_not_found", "The media asset was not found.", ApplicationErrorType.NotFound);

    private static ApplicationResult<T> Failure<T>(string code, string message) =>
        ApplicationResult<T>.Failure(new ApplicationError(code, message, ApplicationErrorType.Validation));

    private static ApplicationResult<T> Conflict<T>(string code, string message) =>
        ApplicationResult<T>.Failure(new ApplicationError(code, message, ApplicationErrorType.Conflict));
}
