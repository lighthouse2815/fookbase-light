using Fookbase.Api.Modules.Media.Config;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Minio;
using Minio.DataModel.Args;

namespace Fookbase.Api.Modules.Media.HealthChecks;

public sealed class MinioBucketHealthCheck(IMinioClient client, MinioOptions options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.BucketExistsAsync(
                    new BucketExistsArgs().WithBucket(options.BucketName),
                    cancellationToken)
                ? HealthCheckResult.Healthy($"MinIO bucket '{options.BucketName}' is available.")
                : HealthCheckResult.Unhealthy($"MinIO bucket '{options.BucketName}' does not exist.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("MinIO is unavailable.", exception);
        }
    }
}
