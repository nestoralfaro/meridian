using Meridian.Application.Accounts;
using Meridian.Application.Auditing;
using Meridian.Application.DemoData;
using Meridian.Application.Identity;
using Meridian.Application.Quotes;
using Meridian.Application.Transfers;
using Meridian.Infrastructure.DemoData;
using Meridian.Infrastructure.Identity;
using Meridian.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
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

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                // NIST SP 800-63B: length over composition rules.
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<MeridianDbContext>()
            .AddPasswordValidator<CommonPasswordValidator>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IAuditTrail, AuditTrail>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ITransferStore, TransferStore>();
        services.AddScoped<ISecurityCatalog, SecurityCatalog>();

        // Keys that sign session cookies and antiforgery tokens, shared by every replica.
        services.AddDataProtection()
            .SetApplicationName("Meridian")
            .PersistKeysToDbContext<MeridianDbContext>();

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: [ReadinessTag])
            .AddCheck<SchemaHealthCheck>("schema", tags: [ReadinessTag]);

        return services;
    }
}
