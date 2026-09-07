using Fookbase.Media.Application.Abstractions;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Fookbase.Media.Infrastructure.Storage;

internal sealed class MinioObjectStorage(IMinioClient client, MinioOptions options) : IObjectStorage
{
    public Task<string> CreatePresignedPutUrlAsync(string objectKey, TimeSpan expiry,
        CancellationToken cancellationToken = default) =>
        client.PresignedPutObjectAsync(new PresignedPutObjectArgs()
            .WithBucket(options.BucketName).WithObject(objectKey).WithExpiry((int)expiry.TotalSeconds));

    public Task<string> CreatePresignedGetUrlAsync(string objectKey, TimeSpan expiry,
        CancellationToken cancellationToken = default) =>
        client.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(options.BucketName).WithObject(objectKey).WithExpiry((int)expiry.TotalSeconds));

    public async Task<StoredObjectInfo?> GetInfoAsync(string objectKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await client.StatObjectAsync(new StatObjectArgs()
                .WithBucket(options.BucketName).WithObject(objectKey), cancellationToken);
            return new StoredObjectInfo(response.Size, response.ContentType);
        }
        catch (ObjectNotFoundException) { return null; }
        catch (ErrorResponseException exception) when (
            exception.ServerMessage?.Contains("not exist", StringComparison.OrdinalIgnoreCase) == true)
        {
            return null;
        }
    }

    public async Task<byte[]> ReadPrefixAsync(string objectKey, int length,
        CancellationToken cancellationToken = default)
    {
        using var destination = new MemoryStream(length);
        await client.GetObjectAsync(new GetObjectArgs().WithBucket(options.BucketName)
            .WithObject(objectKey).WithOffsetAndLength(0, length)
            .WithCallbackStream((source, token) => source.CopyToAsync(destination, token)), cancellationToken);
        return destination.ToArray();
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default) =>
        client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(options.BucketName)
            .WithObject(objectKey), cancellationToken);
}
