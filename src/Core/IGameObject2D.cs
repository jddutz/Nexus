namespace Nexus.Core;

public interface IGameObject2D : IGameObject
{
    /// <summary>
    /// Gets the transformation matrix composed from this object's position, rotation, and scale.
    /// </summary>
    Matrix4X4<float> TransformationMatrix { get; }

    Vector2D<float> Position { get; set; }

    float Rotation { get; set; }

    Vector2D<float> Scale { get; set; }
}
