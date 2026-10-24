using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Meridian.Infrastructure.Persistence;

/// <summary>Readiness probe: the app can open a connection and run a trivial query.</summary>
internal sealed class DatabaseHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException or InvalidOperationException)
        {
            // The exception is attached for logs only; the public health response never includes it.
            return new HealthCheckResult(context.Registration.FailureStatus, "Database unreachable.", ex);
        }
    }
}
