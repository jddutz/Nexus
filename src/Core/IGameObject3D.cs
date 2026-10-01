namespace Nexus.Core;

public interface IGameObject3D : IGameObject, ISpatialObject
{
    /// <summary>Gets the transform relative to the parent.</summary>
    Matrix4X4<float> LocalTransform { get; }

    /// <summary>Gets or sets the position relative to the parent.</summary>
    Vector3D<float> Position { get; set; }

    /// <summary>Gets or sets the rotation relative to the parent.</summary>
    Quaternion<float> Quaternion { get; set; }

    /// <summary>Gets or sets the scale relative to the parent.</summary>
    Vector3D<float> Scale { get; set; }
}
