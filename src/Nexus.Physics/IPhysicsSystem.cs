namespace Nexus.Physics;

public interface IPhysicsSystem
{
    /// <summary>
    /// Updates the physics system for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time since the previous frame.</param>
    void Update(TimeSpan deltaTime);
}
