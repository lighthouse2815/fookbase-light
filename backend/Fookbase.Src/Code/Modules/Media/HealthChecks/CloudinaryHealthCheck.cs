using CloudinaryDotNet;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Fookbase.Api.Modules.Media.HealthChecks;

public sealed class CloudinaryHealthCheck(Cloudinary cloudinary) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await cloudinary.PingAsync(cancellationToken);
            return result.Error is null ? HealthCheckResult.Healthy("Cloudinary is available.")
                : HealthCheckResult.Unhealthy(result.Error.Message);
        }
        catch (Exception exception) { return HealthCheckResult.Unhealthy("Cloudinary is unavailable.", exception); }
    }
}
