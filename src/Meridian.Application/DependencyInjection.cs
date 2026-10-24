using Microsoft.Extensions.DependencyInjection;

namespace Meridian.Application;

public static class DependencyInjection
{
    /// <summary>Registers use cases and application services.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
