using Silk.NET.Maths;

namespace Nexus.GameModel;

public interface IScene
{
    SceneId Id { get; }

    Vector4D<float> BackgroundColor { get; }

    /// <summary>
    /// Updates the scene for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous frame.</param>
    void Update(double deltaTime);
}
