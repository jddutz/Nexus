namespace Nexus.Audio;

using Nexus.Core.Performance;

/// <summary>
/// Provides the default audio system implementation.
/// </summary>
/// <param name="eventHub">The event hub used to register the audio system.</param>
/// <param name="telemetry">The optional performance telemetry sink.</param>
public sealed class AudioSystem(
    IEventHub eventHub,
    IPerformanceTelemetry? telemetry = null
) : IAudioSystem
{
    /// <inheritdoc />
    public void Initialize()
    {
        using var timing = new LoadPerformanceScope(
            telemetry,
            "startup.system.initialize",
            "audio"
        );
        eventHub.Register(this);
    }

    /// <inheritdoc />
    public void Update(double deltaTime) { }

    /// <inheritdoc />
    public bool Activate<TComponent>(TComponent component)
        where TComponent : class, IAudioComponent
    {
        return false;
    }

    /// <inheritdoc />
    public bool Deactivate<TComponent>(TComponent component)
        where TComponent : class, IAudioComponent
    {
        return false;
    }
}
