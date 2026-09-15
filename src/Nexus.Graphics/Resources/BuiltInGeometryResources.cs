namespace Nexus.Graphics.Resources;

public static partial class BuiltInResource
{
    public static readonly IResourceDescription FullScreenTriangleMesh =
        new VertexGeometryResourceDescription(
            "FullScreenTriangleMesh",
            [new Vertex(-1f, -1f, 0f), new Vertex(3f, -1f, 0f), new Vertex(-1f, 3f, 0f)]
        );
}
