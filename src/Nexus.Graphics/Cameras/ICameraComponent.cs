namespace Nexus.Graphics.Cameras;

public interface ICameraComponent : IComponent
{
    Matrix4X4<float> ViewProjectionMatrix { get; }
}
