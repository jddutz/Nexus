namespace Nexus.Graphics.Geometry;

public class UniformColorVertexGeometry : IResourceDescription, IGeometry
{
    public ResourceId Id { get; }
    public string Name { get; }
    public ImmutableArray<Vertex> Vertices { get; }
    public PrimitiveTopologyEnum Topology { get; }

    public IGeometrySource Source => new VertexGeometrySource<Vertex>([.. Vertices]);

    public UniformColorVertexGeometry(
        string name,
        Vertex[] vertices,
        PrimitiveTopologyEnum topology = PrimitiveTopologyEnum.TriangleList
    )
    {
        Name = name;
        Vertices = [.. vertices];
        Topology = topology;

        var hash = new IdentityHashBuilder(nameof(UniformColorVertexGeometry))
            .Add(Name)
            .Add((int)Topology);

        foreach (var vertex in Vertices)
        {
            hash.AddRange([vertex.X, vertex.Y, vertex.Z]);
        }

        Id = hash.Compute();
    }
}
