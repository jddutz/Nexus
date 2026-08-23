namespace Nexus.Testing;

/// <summary>
/// Builds a minimal Nexus runtime for testing without registering
/// the standard engine subsystems.
/// </summary>
public class TestRuntimeBuilder : IRuntimeBuilder
{
    protected IServiceCollection Services { get; }

    public TestRuntimeBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Services = services;
    }

    /// <summary>
    /// Builds a minimal runtime using only the services explicitly
    /// registered for the test environment.
    /// </summary>
    public virtual IRuntime Build()
    {
        var serviceProvider = Services.BuildServiceProvider();

        return new NexusRuntime(serviceProvider);
    }
}
