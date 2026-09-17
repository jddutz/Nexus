namespace Nexus.Core;

public interface IGameObject3D : IGameObject
{
    Vector3D<float> Position { get; set; }

    Quaternion<float> Quaternion { get; set; }

    Vector3D<float> Scale { get; set; }
}
