namespace Nexus.Graphics.Geometry;

/// <summary>
/// Geometry composed of <see cref="TexturedVertex2d"/> records, matching the vertex layout expected
/// by the textured-quad pipeline (2D position plus texture coordinate).
/// </summary>
public class TexturedVertex2dGeometry : IGeometry
{
    public ResourceId Id { get; }
    public string Name { get; }
    public ImmutableArray<TexturedVertex2d> Vertices { get; }
    public PrimitiveTopologyEnum Topology { get; }

    public IGeometrySource Source => new VertexGeometrySource<TexturedVertex2d>([.. Vertices]);

    public TexturedVertex2dGeometry(
        string name,
        TexturedVertex2d[] vertices,
        PrimitiveTopologyEnum topology = PrimitiveTopologyEnum.TriangleStrip
    )
    {
        Name = name;
        Vertices = [.. vertices];
        Topology = topology;

        var hash = new IdentityHashBuilder(nameof(TexturedVertex2dGeometry))
            .Add(Name)
            .Add((int)Topology);

        foreach (var vertex in Vertices)
        {
            hash.AddRange([vertex.X, vertex.Y, vertex.U, vertex.V]);
        }

        Id = hash.Compute();
    }
}
