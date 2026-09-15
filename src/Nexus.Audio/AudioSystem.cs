namespace Nexus.Audio;

/// <summary>
/// Provides the default audio system implementation.
/// </summary>
public sealed class AudioSystem : IAudioSystem
{
    public void Initialize() { }

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
