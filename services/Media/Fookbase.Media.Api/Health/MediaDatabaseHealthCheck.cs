using Fookbase.Media.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Fookbase.Media.Api.Health;

public sealed class MediaDatabaseHealthCheck(MediaDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy("media_db is reachable.")
            : HealthCheckResult.Unhealthy("media_db is unreachable.");
}
