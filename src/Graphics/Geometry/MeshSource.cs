namespace Nexus.Graphics.Geometry;

public sealed class MeshSource : IVertexDataSource
{
    private readonly Vertex[] _vertices;

    public GraphicsId Id { get; }
    public ulong Count => (ulong)_vertices.Length;

    public MeshSource(Vertex[] vertices)
    {
        _vertices = vertices;

        var hash = new IdentityHashBuilder(nameof(MeshSource));

        foreach (var vertex in vertices)
        {
            hash.Add(vertex.Position.X)
                .Add(vertex.Position.Y)
                .Add(vertex.Position.Z)
                .Add(vertex.Normal.X)
                .Add(vertex.Normal.Y)
                .Add(vertex.Normal.Z)
                .Add(vertex.Color.R)
                .Add(vertex.Color.G)
                .Add(vertex.Color.B)
                .Add(vertex.Color.A)
                .Add(vertex.TexCoord.X)
                .Add(vertex.TexCoord.Y);
        }

        Id = hash.Compute();
    }

    public ReadOnlyMemory<byte> GetVertexData(VertexFormat format, PrimitiveTopologyEnum topology)
    {
        if (_vertices.Length == 0)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        var stride = (int)format.Stride;

        var data = new byte[stride * _vertices.Length];

        for (var index = 0; index < _vertices.Length; index++)
        {
            _vertices[index].WriteVertexData(data.AsSpan(index * stride, stride), format);
        }

        return data;
    }
}
