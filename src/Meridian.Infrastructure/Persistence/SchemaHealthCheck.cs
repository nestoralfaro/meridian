using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Meridian.Infrastructure.Persistence;

/// <summary>
/// Readiness probe: every migration this build knows about has been applied. A new revision whose migrations
/// didn't run never becomes ready, so it never receives traffic. (A database that is <i>ahead</i> of this build is fine:
/// migrations are backward compatible, so the previous revision keeps serving during a deployment.)
/// </summary>
internal sealed class SchemaHealthCheck(MeridianDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
            return pending.Count == 0
                ? HealthCheckResult.Healthy()
                : new HealthCheckResult(context.Registration.FailureStatus, $"{pending.Count} pending migration(s): {string.Join(", ", pending)}");
        }
        catch (Exception ex) when (ex is Npgsql.NpgsqlException or TimeoutException or InvalidOperationException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Schema version could not be read.", ex);
        }
    }
}
