namespace Nexus.Graphics.Vulkan;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVkGraphicsServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IWindowService, VulkanWindowService>();
        services.TryAddSingleton(sp => sp.GetRequiredService<IWindowService>().GetWindow());

        services.TryAddSingleton<Context>();
        services.TryAddSingleton<SwapChain>();
        services.TryAddSingleton<ISyncManager, SyncManager>();
        services.TryAddSingleton<IPipelineManager, PipelineManager>();
        services.TryAddSingleton<IRenderer, Renderer>();
        services.TryAddSingleton<ISwapChain, SwapChain>();
        services.TryAddSingleton<IGraphicsSystem, VulkanGraphicsSystem>();
        services.TryAddSingleton<IGraphicsResourceManager, VulkanResourceManager>();

        return services;
    }

    public static IServiceCollection AddVkValidation(this IServiceCollection services)
    {
        services.TryAddSingleton<IValidation, Validation>();

        return services;
    }

    /// <summary>
    /// Register all IAction implementations from the specified assembly
    /// </summary>
    public static IServiceCollection AddDiscoveredServices<T>(
        this IServiceCollection services,
        Assembly assembly
    )
    {
        var types = assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(T).IsAssignableFrom(t));

        foreach (var type in types)
        {
            services.AddTransient(type);
        }

        return services;
    }

    /// <summary>
    /// Register all services of the specified type (typically an interface)
    /// from the calling assembly and the Engine.Actions assembly
    /// </summary>
    public static IServiceCollection AddDiscoveredServices<T>(this IServiceCollection services)
    {
        // Register actions from the assembly that defines the type
        services.AddDiscoveredServices<T>(typeof(T).Assembly);

        // Register actions from the calling assembly
        var callingAssembly = Assembly.GetCallingAssembly();
        if (callingAssembly != typeof(T).Assembly)
        {
            services.AddDiscoveredServices<T>(callingAssembly);
        }

        return services;
    }
}
