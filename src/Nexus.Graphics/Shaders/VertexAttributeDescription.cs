namespace Nexus.Graphics.Shaders;

public readonly record struct VertexAttributeDescription
{
    public ResourceId Id { get; }
    public VertexSemanticEnum Semantic { get; }
    public VertexFormatEnum Format { get; }
    public uint Location { get; }
    public uint Offset { get; }

    public VertexAttributeDescription(
        VertexSemanticEnum semantic,
        VertexFormatEnum format,
        uint location = 0,
        uint offset = 0
    )
    {
        Semantic = semantic;
        Format = format;
        Location = location;
        Offset = offset;

        Id = new IdentityHashBuilder(nameof(VertexAttributeDescription))
            .Add((int)Semantic)
            .Add((int)Format)
            .Add(Location)
            .Add(Offset)
            .Compute();
    }
}
