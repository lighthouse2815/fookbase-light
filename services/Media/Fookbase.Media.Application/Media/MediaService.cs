using Fookbase.Media.Application.Abstractions;
using Fookbase.Media.Application.Common;
using Fookbase.Media.Domain.Entities;

namespace Fookbase.Media.Application.Media;

public sealed class MediaService(
    IMediaRepository repository,
    IObjectStorage objectStorage,
    TimeProvider timeProvider) : IMediaService
{
    public const long MaximumFileSize = 25 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> AllowedContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp",
            ["image/gif"] = ".gif"
        };

    public async Task<ApplicationResult<MediaResponse>> UploadAsync(
        Guid ownerUserId,
        Stream content,
        string fileName,
        string contentType,
        long length,
        string purpose,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateUpload(fileName, contentType, length, purpose, out var parsedPurpose);
        if (validationError is not null)
        {
            return ApplicationResult<MediaResponse>.Failure(validationError);
        }

        var id = Guid.NewGuid();
        var objectName = $"{ownerUserId:N}/{id:N}{AllowedContentTypes[contentType]}";
        var createdAt = timeProvider.GetUtcNow();
        var asset = MediaAsset.Create(
            id,
            ownerUserId,
            objectName,
            Path.GetFileName(fileName),
            contentType.ToLowerInvariant(),
            length,
            parsedPurpose,
            createdAt);

        await objectStorage.PutAsync(
            objectName,
            content,
            length,
            asset.ContentType,
            cancellationToken);

        try
        {
            await repository.AddAsync(asset, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await TryDeleteObjectAsync(objectName, cancellationToken);
            throw;
        }

        return ApplicationResult<MediaResponse>.Success(ToResponse(asset));
    }

    public async Task<ApplicationResult<MediaDownload>> DownloadAsync(
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        var asset = await repository.FindAsync(mediaId, cancellationToken: cancellationToken);
        if (asset is null || asset.DeletedAt is not null)
        {
            return ApplicationResult<MediaDownload>.Failure(NotFound());
        }

        var stream = await objectStorage.OpenReadAsync(asset.ObjectName, cancellationToken);
        return ApplicationResult<MediaDownload>.Success(
            new MediaDownload(stream, asset.ContentType, asset.OriginalFileName));
    }

    public async Task<ApplicationResult> DeleteAsync(
        Guid ownerUserId,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        var asset = await repository.FindAsync(
            mediaId,
            trackChanges: true,
            cancellationToken);
        if (asset is null || asset.DeletedAt is not null)
        {
            return ApplicationResult.Failure(NotFound());
        }

        if (asset.OwnerUserId != ownerUserId)
        {
            return ApplicationResult.Failure(
                new ApplicationError(
                    "media_forbidden",
                    "Only the media owner can delete this file.",
                    ApplicationErrorType.Forbidden));
        }

        asset.Delete(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        await objectStorage.DeleteAsync(asset.ObjectName, cancellationToken);
        return ApplicationResult.Success();
    }

    private async Task TryDeleteObjectAsync(
        string objectName,
        CancellationToken cancellationToken)
    {
        try
        {
            await objectStorage.DeleteAsync(objectName, cancellationToken);
        }
        catch
        {
            // Preserve the original metadata persistence failure.
        }
    }

    private static ApplicationError? ValidateUpload(
        string fileName,
        string contentType,
        long length,
        string purpose,
        out MediaPurpose parsedPurpose)
    {
        parsedPurpose = default;

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Validation("file_required", "A file name is required.");
        }

        if (!AllowedContentTypes.ContainsKey(contentType))
        {
            return Validation(
                "unsupported_media_type",
                "Only JPEG, PNG, WebP and GIF images are supported.");
        }

        if (length <= 0 || length > MaximumFileSize)
        {
            return Validation(
                "invalid_file_size",
                $"The image must contain data and cannot exceed {MaximumFileSize / 1024 / 1024} MB.");
        }

        if (!Enum.TryParse(purpose, ignoreCase: true, out parsedPurpose) ||
            !Enum.IsDefined(parsedPurpose))
        {
            return Validation(
                "invalid_media_purpose",
                "Purpose must be avatar, cover or post.");
        }

        return null;
    }

    private static ApplicationError Validation(string code, string message) =>
        new(code, message, ApplicationErrorType.Validation);

    private static ApplicationError NotFound() =>
        new(
            "media_not_found",
            "The media file was not found.",
            ApplicationErrorType.NotFound);

    private static MediaResponse ToResponse(MediaAsset asset) =>
        new(
            asset.Id,
            asset.OwnerUserId,
            asset.OriginalFileName,
            asset.ContentType,
            asset.Size,
            asset.Purpose.ToString().ToLowerInvariant(),
            $"/api/media/{asset.Id}",
            asset.CreatedAt);
}
