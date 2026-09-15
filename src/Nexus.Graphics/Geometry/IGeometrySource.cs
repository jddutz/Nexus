namespace Nexus.Graphics.Geometry;

public interface IGeometrySource
{
    ReadOnlyMemory<byte> GetVertexData();
}
