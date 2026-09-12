namespace Nexus.Audio;

public interface IAudioService
{
    /// <summary>
    /// Updates the audio service for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time since the previous frame.</param>
    void Update(TimeSpan deltaTime);
}
