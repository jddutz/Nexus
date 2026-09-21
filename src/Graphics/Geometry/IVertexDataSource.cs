namespace Nexus.Graphics.Geometry;

public interface IVertexDataSource
{
    GraphicsId Id { get; }
    ulong Count { get; }

    ReadOnlyMemory<byte> GetVertexData(VertexFormat format, PrimitiveTopologyEnum topology);
}
