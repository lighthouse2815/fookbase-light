using Fookbase.Api.Modules.Media.Config;
using Fookbase.Api.Modules.Media.Services;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Fookbase.Api.Modules.Media.Services;

internal sealed class MinioObjectStorage(
    IMinioClient client,
    MinioPresignedUrlClient presignedUrlClient,
    MinioOptions options) : IObjectStorage
{
    public Task<string> CreatePresignedPutUrlAsync(string objectKey, TimeSpan expiry,
        CancellationToken cancellationToken = default) =>
        presignedUrlClient.Client.PresignedPutObjectAsync(new PresignedPutObjectArgs()
            .WithBucket(options.BucketName).WithObject(objectKey).WithExpiry((int)expiry.TotalSeconds));

    public Task<string> CreatePresignedGetUrlAsync(string objectKey, TimeSpan expiry,
        CancellationToken cancellationToken = default) =>
        presignedUrlClient.Client.PresignedGetObjectAsync(new PresignedGetObjectArgs()
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
        var prefix = new byte[length];
        var bytesRead = 0;
        await client.GetObjectAsync(new GetObjectArgs().WithBucket(options.BucketName)
            .WithObject(objectKey)
            .WithCallbackStream(async (source, token) =>
            {
                while (bytesRead < prefix.Length)
                {
                    var read = await source.ReadAsync(prefix.AsMemory(bytesRead), token);
                    if (read == 0) break;
                    bytesRead += read;
                }
            }), cancellationToken);
        return prefix[..bytesRead];
    }

    public async Task DownloadToFileAsync(
        string objectKey,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        await using var destination = File.Create(destinationPath);
        await client.GetObjectAsync(new GetObjectArgs().WithBucket(options.BucketName)
            .WithObject(objectKey)
            .WithCallbackStream(async (source, token) =>
                await source.CopyToAsync(destination, token)), cancellationToken);
    }

    public async Task UploadFileAsync(
        string objectKey,
        string sourcePath,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        await using var source = File.OpenRead(sourcePath);
        await client.PutObjectAsync(new PutObjectArgs().WithBucket(options.BucketName)
            .WithObject(objectKey)
            .WithStreamData(source)
            .WithObjectSize(source.Length)
            .WithContentType(contentType), cancellationToken);
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default) =>
        client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(options.BucketName)
            .WithObject(objectKey), cancellationToken);
}
