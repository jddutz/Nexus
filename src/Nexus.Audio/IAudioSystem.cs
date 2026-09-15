namespace Nexus.Audio;

public interface IAudioSystem
{
    /// <summary>
    /// Initializes the audio system before the update loop begins.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Updates the audio system for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous frame.</param>
    void Update(double deltaTime);

    bool CanActivate<TComponent>(TComponent component)
        where TComponent : class, IComponent;

    bool Activate<TComponent>(TComponent component)
        where TComponent : class, IComponent;

    bool Deactivate<TComponent>(TComponent component)
        where TComponent : class, IComponent;
}
