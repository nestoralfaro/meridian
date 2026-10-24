using Microsoft.Extensions.DependencyInjection;

namespace Meridian.Infrastructure;

/// <summary>
/// The only public type in this assembly: hosts wire infrastructure through this entry point
/// and never reference adapter types directly.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Registers persistence and external-service adapters.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
