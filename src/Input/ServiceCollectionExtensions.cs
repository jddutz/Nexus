namespace Nexus.Input;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Registers services owned by Nexus.Input.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default input system and its Silk.NET input adapter.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddInputServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<Silk.NET.Input.IInputContext>(serviceProvider =>
            serviceProvider.GetRequiredService<Silk.NET.Windowing.IWindow>().CreateInput()
        );
        services.TryAddSingleton<IInputAdapter, InputAdapter>();
        services.TryAddSingleton<IInputSystem, InputSystem>();

        return services;
    }
}
