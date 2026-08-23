namespace Nexus.Runtime;

/// <summary>
/// Implements the main application entry point for the Nexus Game Engine runtime.
/// </summary>
public sealed class Application(IRuntime runtime) : IApplication
{
    /// <inheritdoc />
    public void Run()
    {
        runtime.Initialize();

        var windowService = runtime.Services.GetRequiredService<IWindowService>();
        var window = windowService.GetOrCreateWindow();

        window.Run();
    }
}
