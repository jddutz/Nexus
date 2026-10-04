namespace Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nexus.Game;

/// <summary>Creates scene registries configured for isolated registry tests.</summary>
internal static class SceneRegistryTestHelper
{
    /// <summary>Creates a registry with automatic assembly scanning disabled.</summary>
    /// <returns>An empty registry for manually registered test scenes.</returns>
    internal static SceneRegistry CreateEmptyRegistry() =>
        new(
            EmptyServiceProvider.Instance,
            Options.Create(new SceneRegistrySettings { ScanEntryAssembly = false })
        );

    /// <summary>Creates a service provider whose scene settings scan the specified type's assembly.</summary>
    /// <typeparam name="T">A type in the assembly to scan.</typeparam>
    /// <returns>A service provider with configured scene-discovery options.</returns>
    internal static ServiceProvider CreateServicesScanningAssemblyContaining<T>()
    {
        var services = new ServiceCollection();
        services.Configure<SceneRegistrySettings>(settings =>
        {
            settings.ScanEntryAssembly = false;
            settings.AddAssemblyContaining<T>();
        });
        return services.BuildServiceProvider();
    }

    /// <summary>Provides no services to scenes created by a test registry.</summary>
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        /// <summary>Gets the shared provider instance.</summary>
        public static EmptyServiceProvider Instance { get; } = new();

        /// <summary>Returns no service.</summary>
        /// <param name="serviceType">The requested service type.</param>
        /// <returns>Always <see langword="null"/>.</returns>
        public object? GetService(Type serviceType) => null;
    }
}
