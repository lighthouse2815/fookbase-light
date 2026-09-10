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

        for (var attempt = 1; attempt <= options.BucketInitializationMaxAttempts; attempt++)
        {
            try
            {
                await EnsurePrivateBucketAsync(cancellationToken);
                logger.LogInformation("Verified private MinIO bucket {BucketName}.", options.BucketName);
                return;
            }
            catch (Exception exception) when (attempt < options.BucketInitializationMaxAttempts)
            {
                logger.LogWarning(
                    exception,
                    "MinIO bucket bootstrap attempt {Attempt}/{MaximumAttempts} failed; retrying in {DelaySeconds}s.",
                    attempt,
                    options.BucketInitializationMaxAttempts,
                    options.BucketInitializationRetrySeconds);
                await Task.Delay(TimeSpan.FromSeconds(options.BucketInitializationRetrySeconds), cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogCritical(exception, "MinIO bucket bootstrap failed after {MaximumAttempts} attempts; Media startup is aborted.", options.BucketInitializationMaxAttempts);
                throw;
            }
        }
    }

    private async Task EnsurePrivateBucketAsync(CancellationToken cancellationToken)
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
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
