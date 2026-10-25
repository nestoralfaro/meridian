using Meridian.Application.DemoData;
using Meridian.Infrastructure.DemoData;
using Meridian.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Meridian.Infrastructure;

/// <summary>
/// The only public type in this assembly: hosts wire infrastructure through this entry point
/// and never reference adapter types directly.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Health-check tag for dependencies that must be up before the app receives traffic.</summary>
    public const string ReadinessTag = "ready";

    private const string ConnectionStringName = "Meridian";

    /// <summary>Registers persistence and external-service adapters.</summary>
    /// <exception cref="InvalidOperationException">The <c>Meridian</c> connection string is not configured.</exception>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. Set ConnectionStrings__{ConnectionStringName}.");
        }

        services.AddNpgsqlDataSource(connectionString, dataSource =>
        {
            // Neon doesn't use Kerberos, and the chiseled runtime image has no libgssapi:
            // probing for GSS encryption only costs a round trip and logs a native-library error.
            dataSource.ConnectionStringBuilder.GssEncryptionMode = GssEncryptionMode.Disable;
        });

        services.AddDbContext<MeridianDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>()));

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IDemoData, DemoDataSeeder>();

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: [ReadinessTag])
            .AddCheck<SchemaHealthCheck>("schema", tags: [ReadinessTag]);

        return services;
    }
}
