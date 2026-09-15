namespace Nexus.Graphics.Vulkan.Resources;

public sealed class UniformColorVertexGeometryDefinition : IResourceDefinition
{
    public ResourceId Id { get; }
    public ImmutableArray<Vertex> Vertices { get; }

    public UniformColorVertexGeometryDefinition(ImmutableArray<Vertex> vertices)
    {
        Vertices = vertices;

        var hash = new IdentityHashBuilder(nameof(UniformColorVertexGeometryDefinition));

        foreach (var vertex in Vertices)
        {
            hash.Add(vertex.X).Add(vertex.Y).Add(vertex.Z);
        }

        Id = hash.Compute();
    }
}
