namespace Nexus.Graphics.Geometry;

public sealed class VertexGeometrySource<TVertex>(TVertex[] vertices) : IGeometrySource
    where TVertex : unmanaged
{
    private readonly TVertex[] _vertices = vertices;

    public ReadOnlyMemory<byte> GetVertexData()
    {
        return MemoryMarshal.AsBytes(_vertices.AsSpan()).ToArray();
    }
}
