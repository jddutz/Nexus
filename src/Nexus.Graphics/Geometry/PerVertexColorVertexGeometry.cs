namespace Nexus.Graphics.Geometry;

public class PerVertexColorVertexGeometry : IResourceDescription, IGeometry
{
    public ResourceId Id { get; }
    public string Name { get; }
    public ImmutableArray<Vertex> Vertices { get; }
    public ImmutableArray<Color> Colors { get; }
    public PrimitiveTopologyEnum Topology { get; }

    public IGeometrySource Source => new VertexGeometrySource<Vertex>([.. Vertices]);

    public PerVertexColorVertexGeometry(
        string name,
        Vertex[] vertices,
        Color[] colors,
        PrimitiveTopologyEnum topology = PrimitiveTopologyEnum.TriangleList
    )
    {
        Name = name;
        Vertices = [.. vertices];
        Colors = [.. colors];
        Topology = topology;

        var hash = new IdentityHashBuilder(nameof(PerVertexColorVertexGeometry))
            .Add(Name)
            .Add((int)Topology);

        foreach (var vertex in Vertices)
        {
            hash.AddRange([vertex.X, vertex.Y, vertex.Z]);
        }

        foreach (var color in Colors)
        {
            hash.AddRange([color.R, color.G, color.B, color.A]);
        }

        Id = hash.Compute();
    }
}
