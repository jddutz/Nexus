namespace Nexus.Graphics.Geometry;

public interface IMeshSource
{
    ResourceId Id { get; }
    ulong Count { get; }

    ReadOnlyMemory<byte> GetVertexData(VertexFormat format);
}
