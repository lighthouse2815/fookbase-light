using Fookbase.Api.Modules.Media.Config;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Fookbase.Api.Modules.Media.Background;

internal sealed class MinioBucketInitializer(
    IMinioClient client, MinioOptions options, ILogger<MinioBucketInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.BucketInitializationEnabled) return;
        try
        {
            if (!await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(options.BucketName), cancellationToken))
                await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(options.BucketName), cancellationToken);

            try
            {
                await client.RemovePolicyAsync(new RemovePolicyArgs().WithBucket(options.BucketName), cancellationToken);
            }
            catch (ErrorResponseException exception) when (
                exception.ServerMessage?.Contains("policy", StringComparison.OrdinalIgnoreCase) == true)
            {
                // A bucket without a policy is already private.
            }

            if (!await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(options.BucketName), cancellationToken))
                throw new InvalidOperationException($"MinIO bucket '{options.BucketName}' bootstrap could not be verified.");
            logger.LogInformation("Verified private MinIO bucket {BucketName}.", options.BucketName);
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "MinIO bucket bootstrap failed; Media startup is aborted.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
