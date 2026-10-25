using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API in memory with a chosen database and the SPA test fixture as web root.
/// </summary>
public sealed class MeridianApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    private static readonly string SpaFixturePath = Path.Combine(AppContext.BaseDirectory, "SpaFixture");
    private Action<IServiceCollection> _testServices = _ => { };
    private readonly Dictionary<string, string> _settings = [];

    /// <summary>Overrides a configuration value (as an environment variable would in Azure).</summary>
    public MeridianApiFactory WithSetting(string key, string value)
    {
        _settings[key] = value;
        return this;
    }

    /// <summary>Adds test-only services (exporters, log collectors, startup filters) before the host starts.</summary>
    public MeridianApiFactory WithTestServices(Action<IServiceCollection> configure)
    {
        _testServices += configure;
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Meridian", connectionString);
        builder.UseWebRoot(SpaFixturePath);
        foreach (var (key, value) in _settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services => _testServices(services));
    }

    public static async Task<MeridianApiFactory> WithDatabaseAsync() =>
        new(await TestDatabase.GetConnectionStringAsync().ConfigureAwait(false));

    public static MeridianApiFactory WithUnreachableDatabase() => new(TestDatabase.Unreachable);

    public static async Task<MeridianApiFactory> WithUnmigratedDatabaseAsync() =>
        new(await TestDatabase.GetUnmigratedConnectionStringAsync().ConfigureAwait(false));
}
