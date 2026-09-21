namespace Nexus.Audio;

/// <summary>
/// Provides the default audio system implementation.
/// </summary>
public sealed class AudioSystem(IEventHub eventHub) : IAudioSystem
{
    public void Initialize()
    {
        eventHub.Register(this);
    }

    /// <inheritdoc />
    public void Update(double deltaTime) { }

    public bool Activate<TComponent>(TComponent component)
        where TComponent : class, IAudioComponent
    {
        return false;
    }

    public bool Deactivate<TComponent>(TComponent component)
        where TComponent : class, IAudioComponent
    {
        return false;
    }
}
