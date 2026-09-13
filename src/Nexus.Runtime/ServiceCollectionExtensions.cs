using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Nexus.Graphics.Vulkan.Pipelines;
using Nexus.Graphics.Vulkan.Synchronization;
using Nexus.Runtime;

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

        return services;
    }

    public static IServiceCollection AddVkValidation(this IServiceCollection services)
    {
        services.TryAddSingleton<IValidation, Validation>();

        return services;
    }
}
