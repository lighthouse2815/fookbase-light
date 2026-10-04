using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Media.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Media.Entities;

[Table("MediaAssets")]
[Index(nameof(ObjectKey), IsUnique = true)]
[Index(nameof(OwnerUserId), nameof(CreatedAtUtc))]
[Index(nameof(Status), nameof(UploadExpiresAtUtc))]
[Index(nameof(Status), nameof(CreatedAtUtc))]
public sealed class MediaAsset
{
    public const int MaximumObjectKeyLength = 256;
    public const int MaximumFileNameLength = 255;
    public const int MaximumContentTypeLength = 100;
    public const int MaximumProcessingErrorLength = 1000;

    private MediaAsset()
    {
    }

    public MediaAsset(
        Guid id,
        Guid ownerUserId,
        MediaType mediaType,
        string objectKey,
        string originalFileName,
        string contentType,
        long declaredSizeBytes,
        DateTimeOffset createdAtUtc,
        DateTimeOffset uploadExpiresAtUtc)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        MediaType = mediaType;
        Status = MediaStatus.PENDING_UPLOAD;
        ObjectKey = objectKey;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        DeclaredSizeBytes = declaredSizeBytes;
        CreatedAtUtc = createdAtUtc;
        UploadExpiresAtUtc = uploadExpiresAtUtc;
    }

    [Key]
    public Guid Id { get; private set; }
    // Retain the owner ID on assets that survive account deletion.
    public Guid OwnerUserId { get; private set; }
    public MediaType MediaType { get; private set; }
    public MediaStatus Status { get; private set; }

    [MaxLength(MaximumObjectKeyLength)]
    public string ObjectKey { get; private set; } = string.Empty;

    [MaxLength(MaximumFileNameLength)]
    public string OriginalFileName { get; private set; } = string.Empty;

    [MaxLength(MaximumContentTypeLength)]
    public string ContentType { get; private set; } = string.Empty;
    public long DeclaredSizeBytes { get; private set; }
    public long? ActualSizeBytes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UploadExpiresAtUtc { get; private set; }
    public DateTimeOffset? UploadedAtUtc { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }
    public long? DurationMs { get; private set; }
    public int? Width { get; private set; }
    public int? Height { get; private set; }

    [MaxLength(MaximumObjectKeyLength)]
    public string? ProcessedObjectKey { get; private set; }

    [MaxLength(MaximumObjectKeyLength)]
    public string? PosterObjectKey { get; private set; }

    [MaxLength(MaximumProcessingErrorLength)]
    public string? ProcessingError { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    [InverseProperty(nameof(MediaReference.Media))]
    public ICollection<MediaReference> PostReferences { get; private set; } = new List<MediaReference>();

    [InverseProperty(nameof(ProfileMediaReference.Media))]
    public ICollection<ProfileMediaReference> ProfileReferences { get; private set; } = new List<ProfileMediaReference>();

    [InverseProperty(nameof(ObjectDeletion.Media))]
    public ICollection<ObjectDeletion> ObjectDeletions { get; private set; } = new List<ObjectDeletion>();

    [InverseProperty(nameof(MediaProcessingJob.Media))]
    public MediaProcessingJob? ProcessingJob { get; private set; }

    public bool MarkReady(long actualSizeBytes, DateTimeOffset uploadedAtUtc)
    {
        if (Status == MediaStatus.READY)
        {
            return false;
        }

        if (Status != MediaStatus.PENDING_UPLOAD)
        {
            throw new InvalidOperationException("Only a pending upload can become ready.");
        }

        ActualSizeBytes = actualSizeBytes;
        UploadedAtUtc = uploadedAtUtc;
        Status = MediaStatus.READY;
        return true;
    }

    public bool MarkFailed()
    {
        if (Status is not (MediaStatus.PENDING_UPLOAD or MediaStatus.PROCESSING))
        {
            return false;
        }

        Status = MediaStatus.FAILED;
        return true;
    }

    public void MarkProcessing(long actualSizeBytes, DateTimeOffset uploadedAtUtc)
    {
        if (Status != MediaStatus.PENDING_UPLOAD || MediaType != MediaType.VIDEO)
        {
            throw new InvalidOperationException("Only a pending video upload can begin processing.");
        }

        ActualSizeBytes = actualSizeBytes;
        UploadedAtUtc = uploadedAtUtc;
        Status = MediaStatus.PROCESSING;
    }

    public void MarkVideoReady(
        string processedObjectKey,
        string posterObjectKey,
        long durationMs,
        int width,
        int height,
        DateTimeOffset processedAtUtc)
    {
        if (Status != MediaStatus.PROCESSING || MediaType != MediaType.VIDEO ||
            durationMs <= 0 || width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("Only a processed video with valid metadata can become ready.");
        }

        ProcessedObjectKey = processedObjectKey;
        PosterObjectKey = posterObjectKey;
        DurationMs = durationMs;
        Width = width;
        Height = height;
        ProcessedAtUtc = processedAtUtc;
        ProcessingError = null;
        Status = MediaStatus.READY;
    }

    public void MarkProcessingFailed(string error)
    {
        if (Status != MediaStatus.PROCESSING)
        {
            throw new InvalidOperationException("Only a processing video can fail processing.");
        }

        ProcessingError = error.Length <= MaximumProcessingErrorLength ? error : error[..MaximumProcessingErrorLength];
        Status = MediaStatus.FAILED;
    }

    public static string ProcessedKey(Guid ownerUserId, Guid mediaId) =>
        $"{ownerUserId:N}/{mediaId:N}/processed.mp4";

    public static string PosterKey(Guid ownerUserId, Guid mediaId) =>
        $"{ownerUserId:N}/{mediaId:N}/poster.jpg";

    public bool Delete(DateTimeOffset deletedAtUtc)
    {
        if (Status == MediaStatus.DELETED)
        {
            return false;
        }

        if (Status != MediaStatus.READY)
        {
            throw new InvalidOperationException("Only ready media can be deleted.");
        }

        Status = MediaStatus.DELETED;
        DeletedAtUtc = deletedAtUtc;
        return true;
    }
}
