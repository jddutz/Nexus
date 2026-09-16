namespace Nexus.Graphics.Cameras;

public class PerspectiveCamera : Component, ICameraComponent
{
    public Matrix4X4<float> ViewProjectionMatrix { get; }
}
