using Fookbase.Media.Application.Abstractions;
using Minio;
using Minio.DataModel.Args;

namespace Fookbase.Media.Infrastructure.Storage;

internal sealed class MinioObjectStorage(
    IMinioClient client,
    MinioOptions options) : IObjectStorage
{
    public Task PutAsync(
        string objectName,
        Stream content,
        long length,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var arguments = new PutObjectArgs()
            .WithBucket(options.BucketName)
            .WithObject(objectName)
            .WithStreamData(content)
            .WithObjectSize(length)
            .WithContentType(contentType);

        return client.PutObjectAsync(arguments, cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(
        string objectName,
        CancellationToken cancellationToken = default)
    {
        var destination = new MemoryStream();
        var arguments = new GetObjectArgs()
            .WithBucket(options.BucketName)
            .WithObject(objectName)
            .WithCallbackStream((source, token) => source.CopyToAsync(destination, token));

        await client.GetObjectAsync(arguments, cancellationToken);
        destination.Position = 0;
        return destination;
    }

    public Task DeleteAsync(
        string objectName,
        CancellationToken cancellationToken = default)
    {
        var arguments = new RemoveObjectArgs()
            .WithBucket(options.BucketName)
            .WithObject(objectName);

        return client.RemoveObjectAsync(arguments, cancellationToken);
    }
}
