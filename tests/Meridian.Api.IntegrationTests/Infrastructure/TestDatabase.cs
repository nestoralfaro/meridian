using Testcontainers.PostgreSql;

namespace Meridian.Api.IntegrationTests.Infrastructure;

/// <summary>
/// One real PostgreSQL container shared by every test in the run (same major version as Neon).
/// Testcontainers' resource reaper removes it when the test process exits.
/// </summary>
internal static class TestDatabase
{
    /// <summary>Connection string that fails fast: nothing listens on port 1.</summary>
    public const string Unreachable = "Host=127.0.0.1;Port=1;Database=meridian;Username=nobody;Password=none;Timeout=2";

    private static readonly PostgreSqlContainer Container = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private static readonly Lazy<Task> Started = new(() => Container.StartAsync());

    public static async Task<string> GetConnectionStringAsync()
    {
        await Started.Value.ConfigureAwait(false);
        return Container.GetConnectionString();
    }
}
