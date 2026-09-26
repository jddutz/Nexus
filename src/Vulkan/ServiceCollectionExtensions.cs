namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Registers Vulkan graphics services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Vulkan graphics implementation and its dependencies.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddVkGraphicsServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IWindowService, VulkanWindowService>();
        services.TryAddSingleton(sp => sp.GetRequiredService<IWindowService>().GetMainWindow());
        services.TryAddSingleton<PerformanceMetrics>();
        services.TryAddSingleton<PerformanceDiagnostics>();
        services.TryAddSingleton<Context>();
        services.TryAddSingleton<RenderPassConfigurations>();
        services.TryAddSingleton<SwapChain>();
        services.TryAddSingleton<ISyncManager, SyncManager>();
        services.TryAddSingleton<IPipelineFactory, PipelineFactory>();
        services.TryAddSingleton<IPipelineRegistry, PipelineRegistry>();
        services.TryAddSingleton<IVertexBufferRegistry, VertexBufferRegistry>();
        services.TryAddSingleton<IInstanceBufferRegistry, InstanceBufferRegistry>();
        services.TryAddSingleton<IDescriptorSetLayoutFactory, DescriptorSetLayoutFactory>();
        services.TryAddSingleton<IDescriptorSetFactory, DescriptorSetFactory>();
        services.TryAddSingleton<ICommandFactory, CommandFactory>();
        services.TryAddSingleton<IBufferManager, BufferManager>();
        services.TryAddSingleton<IDescriptorSetPool, DescriptorSetPool>();
        services.TryAddSingleton<IRenderer, Renderer>();
        services.TryAddSingleton<ISwapChain, SwapChain>();
        services.TryAddSingleton<IGraphicsSystem, VulkanGraphicsSystem>();
        services.TryAddSingleton<IImageRegistry, ImageRegistry>();
        services.TryAddSingleton<ISamplerRegistry, SamplerRegistry>();
        services.AddSingleton<IShaderFactory, ShaderFactory>();
        services.AddVkValidation();

        return services;
    }

    /// <summary>
    /// Registers Vulkan validation services.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddVkValidation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IValidation, Validation>();
        return services;
    }
}
