namespace Nexus.Core;

public interface ISpatialObject
{
    /// <summary>Gets the local transform composed with spatial ancestors.</summary>
    Matrix4X4<float> WorldTransform { get; }

    /// <summary>Occurs when the world transform changes.</summary>
    event Action<Matrix4X4<float>, Matrix4X4<float>> WorldTransformChanged;
}
