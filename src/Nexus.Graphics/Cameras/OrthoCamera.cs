namespace Nexus.Graphics.Cameras;

public class OrthoCamera : Component, ICameraComponent
{
    public Matrix4X4<float> ViewProjectionMatrix { get; }
}
