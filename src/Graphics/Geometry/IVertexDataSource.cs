namespace Nexus.Graphics.Geometry;

public interface IVertexDataSource
{
    ResourceId Id { get; }
    ulong Count { get; }

    ReadOnlyMemory<byte> GetVertexData(VertexFormat format);
}
