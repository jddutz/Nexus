namespace Nexus.Graphics.Vulkan.Components;

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

public sealed class TexturedVertex2dGeometryDefinition : IResourceDefinition
{
    public ResourceId Id { get; }
    public ImmutableArray<TexturedVertex2d> Vertices { get; }

    public TexturedVertex2dGeometryDefinition(ImmutableArray<TexturedVertex2d> vertices)
    {
        Vertices = vertices;

        var hash = new IdentityHashBuilder(nameof(TexturedVertex2dGeometryDefinition));

        foreach (var vertex in Vertices)
        {
            hash.Add(vertex.X).Add(vertex.Y).Add(vertex.U).Add(vertex.V);
        }

        Id = hash.Compute();
    }
}
