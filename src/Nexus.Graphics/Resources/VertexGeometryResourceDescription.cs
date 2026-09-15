using System.Reflection.PortableExecutable;

namespace Nexus.Graphics.Resources;

public class VertexGeometryResourceDescription : IResourceDescription
{
    public ResourceId Id { get; }
    public string Name { get; }
    public ImmutableArray<Vertex> Vertices { get; }

    public VertexGeometryResourceDescription(string name, Vertex[] vertices)
    {
        Name = name;
        Vertices = [.. vertices];

        var hash = new IdentityHashBuilder(nameof(VertexGeometryResourceDescription)).Add(Name);

        foreach (var vertex in Vertices)
        {
            hash.AddRange([vertex.X, vertex.Y, vertex.Z]);
        }

        Id = hash.Compute();
    }
}
