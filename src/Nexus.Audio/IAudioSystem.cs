namespace Nexus.Audio;

public interface IAudioSystem
{
    /// <summary>
    /// Updates the audio system for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time since the previous frame.</param>
    void Update(TimeSpan deltaTime);
}