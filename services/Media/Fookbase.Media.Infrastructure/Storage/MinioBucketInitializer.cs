using Microsoft.Extensions.Hosting;
using Minio;
using Minio.DataModel.Args;

namespace Fookbase.Media.Infrastructure.Storage;

internal sealed class MinioBucketInitializer(
    IMinioClient client,
    MinioOptions options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var exists = await client.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(options.BucketName),
            cancellationToken);
        if (!exists)
        {
            await client.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(options.BucketName),
                cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
