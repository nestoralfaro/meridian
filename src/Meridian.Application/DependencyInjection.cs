using Meridian.Application.Accounts;
using Meridian.Application.Quotes;
using Meridian.Application.Transfers;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.Application;

public static class DependencyInjection
{
    /// <summary>Registers use cases and application services.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<AccountQueries>();
        services.AddScoped<TransferCommands>();
        services.AddSingleton<QuoteBoard>();
        return services;
    }
}
