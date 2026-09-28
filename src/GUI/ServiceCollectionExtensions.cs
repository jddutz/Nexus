namespace Nexus.GUI;

/// <summary>
/// Registers graphical user interface services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default graphical user interface implementation.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddNexusGui(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IGraphicalUserInterface, GraphicalUserInterface>();
        return services;
    }
}
