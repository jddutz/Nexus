namespace Nexus.Graphics.Components;

public class UniformColorMeshRenderer() : Component, IGraphicsComponent, IGeometryInstance
{
    private HashSet<RenderLayer> _renderLayers = [];
    public IEnumerable<RenderLayer> RenderLayers => _renderLayers;
    public IGeometry? Geometry { get; set; } = null;
    public Matrix4X4<float> TransformationMatrix { get; set; } = Matrix4X4<float>.Identity;
    public Color Color { get; set; } = Colors.Black;
}
