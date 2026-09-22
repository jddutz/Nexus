namespace Nexus.Graphics.Geometry;

public class Mesh : IGeometry
{
    private readonly Vertex[] _vertices;

    public MeshId Id { get; }
    public string Name { get; }
    public PrimitiveTopologyEnum Topology { get; }
    public ulong Count => (ulong)_vertices.Length;

    public Mesh(string name, PrimitiveTopologyEnum topology, Vertex[] vertices)
    {
        Name = name;
        Topology = topology;
        _vertices = vertices;

        var hash = new IdentityHashBuilder(nameof(Mesh)).Add(Name).Add((uint)Topology);

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

    public void WriteTo(int start, int count, VertexFormat format, Span<byte> target)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, _vertices.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, _vertices.Length - start);

        var stride = checked((int)format.Stride);
        var byteCount = checked(count * stride);
        if (target.Length < byteCount)
            throw new ArgumentException("The target span is too small.", nameof(target));

        for (var index = 0; index < count; index++)
            _vertices[start + index].WriteVertexData(target[(index * stride)..], format);
    }
}
