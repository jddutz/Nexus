namespace Nexus.Physics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Registers services owned by Nexus.Physics.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default physics system.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddPhysicsServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IPhysicsSystem, PhysicsSystem>();
        return services;
    }
}
