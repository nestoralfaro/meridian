using Meridian.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Meridian.Api.IntegrationTests.Infrastructure;

/// <summary>
/// One real PostgreSQL 17 container per test run, set up the way production (Neon) is:
/// an owner role that runs migrations and a least-privilege application role the app connects as,
/// using the same grants script as production (db/app-role-grants.sql).
/// Testcontainers' resource reaper removes the container when the test process exits.
/// </summary>
internal static class TestDatabase
{
    /// <summary>Connection string that fails fast: nothing listens on port 1.</summary>
    public const string Unreachable = "Host=127.0.0.1;Port=1;Database=meridian;Username=nobody;Password=none;Timeout=2";

    private const string OwnerRole = "meridiandb_owner";
    private const string OwnerPassword = "owner-test-password";
    private const string AppPassword = "app-test-password";
    private const string MigratedDatabase = "meridiandb";
    private const string UnmigratedDatabase = "meridian_unmigrated";

    private static readonly PostgreSqlContainer Container = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private static readonly Lazy<Task> Ready = new(InitializeAsync);

    /// <summary>The migrated database, as the application role (what the running app uses).</summary>
    public static async Task<string> GetConnectionStringAsync() => await ConnectAsync(MigratedDatabase, MeridianDbContext.ApplicationRole, AppPassword);

    /// <summary>The migrated database, as the owner (what the migration step uses).</summary>
    public static async Task<string> GetOwnerConnectionStringAsync() => await ConnectAsync(MigratedDatabase, OwnerRole, OwnerPassword);

    /// <summary>A database with grants but no migrations applied, as the application role.</summary>
    public static async Task<string> GetUnmigratedConnectionStringAsync() => await ConnectAsync(UnmigratedDatabase, MeridianDbContext.ApplicationRole, AppPassword);

    /// <summary>
    /// A brand-new, migrated database with production grants, for tests that delete or rewrite data.
    /// Returns the application-role connection string.
    /// </summary>
    public static async Task<string> CreateIsolatedDatabaseAsync()
    {
        await Ready.Value.ConfigureAwait(false);
        var name = $"meridian_{Guid.NewGuid():N}";
        await ExecuteAsync(Container.GetConnectionString(), $"CREATE DATABASE {name} OWNER {OwnerRole}").ConfigureAwait(false);
        await ExecuteAsync(Owner(name), await GrantsAsync().ConfigureAwait(false)).ConfigureAwait(false);
        await MigrateAsync(Owner(name)).ConfigureAwait(false);
        return await ConnectAsync(name, MeridianDbContext.ApplicationRole, AppPassword).ConfigureAwait(false);
    }

    private static async Task<string> ConnectAsync(string database, string username, string password)
    {
        await Ready.Value.ConfigureAwait(false);
        return new NpgsqlConnectionStringBuilder(Container.GetConnectionString())
        {
            Database = database,
            Username = username,
            Password = password,
        }.ConnectionString;
    }

    private static async Task InitializeAsync()
    {
        await Container.StartAsync().ConfigureAwait(false);

        // Cluster-level setup (on Neon: the owner exists with the project; meridian_app is created in the SQL Editor).
        // One statement per command: CREATE DATABASE can't run inside the implicit transaction of a batch.
        foreach (var statement in new[]
        {
            $"CREATE ROLE {OwnerRole} WITH LOGIN PASSWORD '{OwnerPassword}'",
            $"CREATE ROLE {MeridianDbContext.ApplicationRole} WITH LOGIN PASSWORD '{AppPassword}'",
            $"CREATE DATABASE {MigratedDatabase} OWNER {OwnerRole}",
            $"CREATE DATABASE {UnmigratedDatabase} OWNER {OwnerRole}",
        })
        {
            await ExecuteAsync(Container.GetConnectionString(), statement).ConfigureAwait(false);
        }

        var grants = await GrantsAsync().ConfigureAwait(false);
        foreach (var database in new[] { MigratedDatabase, UnmigratedDatabase })
        {
            await ExecuteAsync(Owner(database), grants).ConfigureAwait(false);
        }

        // The CD pipeline runs the migration bundle as the owner; the tests apply the same migrations.
        await MigrateAsync(Owner(MigratedDatabase)).ConfigureAwait(false);
    }

    private static Task<string> GrantsAsync() =>
        File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Database", "app-role-grants.sql"));

    private static async Task MigrateAsync(string ownerConnectionString)
    {
        var options = new DbContextOptionsBuilder<MeridianDbContext>().UseNpgsql(ownerConnectionString).Options;
        await using var db = new MeridianDbContext(options);
        await db.Database.MigrateAsync().ConfigureAwait(false);
    }

    private static string Owner(string database) =>
        new NpgsqlConnectionStringBuilder(Container.GetConnectionString()) { Database = database, Username = OwnerRole, Password = OwnerPassword }.ConnectionString;

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
