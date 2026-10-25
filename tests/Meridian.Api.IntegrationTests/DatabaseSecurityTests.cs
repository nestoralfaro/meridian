using Meridian.Api.IntegrationTests.Infrastructure;
using Npgsql;

namespace Meridian.Api.IntegrationTests;

/// <summary>
/// The application connects as a least-privilege role (db/app-role-grants.sql). Even if the app were compromised
/// (e.g. SQL injection), it couldn't change the schema or rewrite the audit trail.
/// </summary>
public sealed class DatabaseSecurityTests
{
    private const string InsufficientPrivilege = "42501";

    [Fact]
    public async Task ApplicationRoleCannotChangeTheSchema()
    {
        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteAsAppAsync("CREATE TABLE intruder (id int)"));

        exception.SqlState.ShouldBe(InsufficientPrivilege);
    }

    [Fact]
    public async Task ApplicationRoleCanAppendToAndReadTheAuditTrail()
    {
        var subject = $"account:{Guid.NewGuid()}";

        await ExecuteAsAppAsync($"""
            INSERT INTO "AuditEvents" ("Id", "Actor", "Action", "Subject") VALUES ('{Guid.CreateVersion7()}', 'user:dana', 'AccountViewed', '{subject}')
            """);
        var count = await ScalarAsAppAsync($"""SELECT count(*) FROM "AuditEvents" WHERE "Subject" = '{subject}'""");

        count.ShouldBe(1L);
    }

    [Theory]
    [InlineData("""UPDATE "AuditEvents" SET "Actor" = 'someone-else'""")]
    [InlineData("""DELETE FROM "AuditEvents" """)]
    [InlineData("""TRUNCATE "AuditEvents" """)]
    public async Task ApplicationRoleCannotRewriteOrEraseTheAuditTrail(string tamper)
    {
        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteAsAppAsync(tamper));

        exception.SqlState.ShouldBe(InsufficientPrivilege);
    }

    private static async Task ExecuteAsAppAsync(string sql)
    {
        await using var command = await CommandAsAppAsync(sql);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<object?> ScalarAsAppAsync(string sql)
    {
        await using var command = await CommandAsAppAsync(sql);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<NpgsqlCommand> CommandAsAppAsync(string sql)
    {
        var connection = new NpgsqlConnection(await TestDatabase.GetConnectionStringAsync());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return new NpgsqlCommand(sql, connection) { AllResultTypesAreUnknown = false };
    }
}
