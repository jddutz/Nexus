using Nexus.Graphics.Vulkan.Textures;

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
        services.TryAddSingleton<IPipelineFactory, PipelineFactory>();
        services.TryAddSingleton<IPipelineRegistry, PipelineRegistry>();
        services.TryAddSingleton<IVertexBufferRegistry, VertexBufferRegistry>();
        services.TryAddSingleton<IDescriptorSetLayoutFactory, DescriptorSetLayoutFactory>();
        services.TryAddSingleton<IBufferManager, BufferManager>();
        services.TryAddSingleton<IDescriptorSetPool, DescriptorSetPool>();
        services.TryAddSingleton<IRenderer, Renderer>();
        services.TryAddSingleton<ISwapChain, SwapChain>();
        services.TryAddSingleton<IGraphicsSystem, VulkanGraphicsSystem>();

        // Don't try to override these, add a new registry / factory combination instead
        services.AddSingleton<IComponentRegistry, ComponentRegistry>();
        services.AddSingleton<ICameraRegistry, CameraRegistry>();
        services.AddSingleton<IShaderFactory, ShaderFactory>();
        services.AddSingleton<IMeshFactory, MeshFactory>();
        services.AddSingleton<IImageRegistry, ImageRegistry>();
        services.AddSingleton<IImageViewRegistry, ImageViewRegistry>();
        services.AddSingleton<ISamplerRegistry, SamplerRegistry>();
        services.AddSingleton<ITextureRegistry, TextureRegistry>();

        services.AddVkValidation();

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
