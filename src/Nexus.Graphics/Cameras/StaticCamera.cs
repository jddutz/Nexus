namespace Nexus.Graphics.Cameras;

public class StaticCamera : Component, ICameraComponent
{
    public Matrix4X4<float> ViewProjectionMatrix { get; }
}
