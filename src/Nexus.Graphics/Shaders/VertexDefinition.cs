namespace Nexus.Graphics.Shaders;

public class VertexDescription
{
    public ResourceId Id { get; }
    public uint Stride { get; init; }
    public IReadOnlyList<VertexAttributeDescription> Attributes { get; init; }

    public VertexDescription(uint stride, IReadOnlyList<VertexAttributeDescription> attributes)
    {
        Stride = stride;
        Attributes = attributes;

        var hash = new IdentityHashBuilder(nameof(VertexDescription)).Add(stride);

        foreach (var attribute in Attributes)
        {
            hash.Add(attribute.Id);
        }

        Id = hash.Compute();
    }
}
