namespace Nexus.Graphics.Geometry;

public sealed class MeshGeometrySource(string path) : IGeometrySource
{
    public string Path { get; set; } = path;

    public ReadOnlyMemory<byte> GetVertexData()
    {
        // Read/decode .mesh vertex data.
        throw new NotImplementedException();
    }
}
