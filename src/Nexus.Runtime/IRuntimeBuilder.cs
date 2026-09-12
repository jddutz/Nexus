namespace Nexus.Runtime;

/// <summary>
/// Defines a builder capable of constructing a configured Nexus runtime.
/// </summary>
public interface IRuntimeBuilder
{
    IRuntimeBuilder AddServices(IServiceCollection services);
    IRuntimeBuilder AddConfiguration(IConfiguration config);

    IRuntimeBuilder UseVulkan();
    IRuntimeBuilder UseOpenGL();

    /// <summary>
    /// Builds the configured runtime.
    /// </summary>
    /// <returns>
    /// A fully configured runtime instance.
    /// </returns>
    INexusRuntime Build();
}
