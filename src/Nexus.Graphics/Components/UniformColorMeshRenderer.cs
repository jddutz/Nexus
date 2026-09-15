namespace Nexus.Graphics.Components;

public class UniformColorMeshRenderer() : Component, IGraphicsComponent
{
    public HashSet<RenderLayer> RenderLayers { get; set; } = [];
    public Matrix4X4<float> TransformationMatrix { get; set; } = Matrix4X4<float>.Identity;
    public Vector4D<float> Color { get; set; } = Colors.Black;
    public IGeometry? Geometry { get; set; } = null;

    public override bool Activate()
    {
        throw new NotImplementedException();
    }

    public override bool Deactivate()
    {
        throw new NotImplementedException();
    }
}
