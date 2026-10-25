using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Meridian.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API in memory with a chosen database and the SPA test fixture as web root.
/// </summary>
public sealed class MeridianApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    private static readonly string SpaFixturePath = Path.Combine(AppContext.BaseDirectory, "SpaFixture");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Meridian", connectionString);
        builder.UseWebRoot(SpaFixturePath);
    }

    public static async Task<MeridianApiFactory> WithDatabaseAsync() =>
        new(await TestDatabase.GetConnectionStringAsync().ConfigureAwait(false));

    public static MeridianApiFactory WithUnreachableDatabase() => new(TestDatabase.Unreachable);

    public static async Task<MeridianApiFactory> WithUnmigratedDatabaseAsync() =>
        new(await TestDatabase.GetUnmigratedConnectionStringAsync().ConfigureAwait(false));
}
