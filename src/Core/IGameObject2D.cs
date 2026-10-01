namespace Nexus.Core;

public interface IGameObject2D : IGameObject
{
    /// <summary>Gets the transform relative to the parent.</summary>
    Matrix4X4<float> LocalTransform { get; }

    /// <summary>Gets the local transform composed with spatial ancestors.</summary>
    Matrix4X4<float> WorldTransform { get; }

    /// <summary>Occurs when the world transform changes.</summary>
    event Action<Matrix4X4<float>, Matrix4X4<float>> WorldTransformChanged;

    /// <summary>Gets or sets the position relative to the parent.</summary>
    Vector2D<float> Position { get; set; }

    /// <summary>Gets or sets the rotation relative to the parent.</summary>
    float Rotation { get; set; }

    /// <summary>Gets or sets the scale relative to the parent.</summary>
    Vector2D<float> Scale { get; set; }
}
