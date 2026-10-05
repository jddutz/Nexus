namespace Nexus.Graphics.Geometry;

public class Mesh : IGeometry
{
    private readonly Vertex[] _vertices;
    private readonly uint[] _indices;
    private readonly IReadOnlyList<uint> _indicesView;

    /// <inheritdoc />
    public GeometryId Id { get; }
    /// <inheritdoc />
    public string Name { get; }
    /// <inheritdoc />
    public PrimitiveTopologyEnum Topology { get; }
    /// <inheritdoc />
    public ulong Count => VertexCount;
    /// <summary>Gets the number of vertices stored by this mesh.</summary>
    public ulong VertexCount => (ulong)_vertices.Length;
    /// <summary>Gets the number of indices used by this mesh, or zero for non-indexed geometry.</summary>
    public ulong IndexCount => (ulong)_indices.Length;
    /// <summary>Gets the validated 32-bit indices used by this mesh.</summary>
    public IReadOnlyList<uint> Indices => _indicesView;

    /// <summary>Creates a non-indexed mesh.</summary>
    /// <param name="name">The display name of the mesh.</param>
    /// <param name="topology">The primitive topology used by the mesh.</param>
    /// <param name="vertices">The vertices stored by the mesh.</param>
    public Mesh(string name, PrimitiveTopologyEnum topology, Vertex[] vertices)
        : this(name, topology, vertices, null) { }

    /// <summary>Creates a mesh with optional validated 32-bit indices.</summary>
    /// <param name="name">The display name of the mesh.</param>
    /// <param name="topology">The primitive topology used by the mesh.</param>
    /// <param name="vertices">The vertices stored by the mesh.</param>
    /// <param name="indices">The optional indices referencing <paramref name="vertices"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when required data is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an index is outside the vertex range.</exception>
    public Mesh(string name, PrimitiveTopologyEnum topology, Vertex[] vertices, uint[]? indices)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(vertices);
        if (indices is not null)
        {
            for (var index = 0; index < indices.Length; index++)
                if (indices[index] >= (uint)vertices.Length)
                    throw new ArgumentOutOfRangeException(
                        nameof(indices),
                        $"Index {indices[index]} at position {index} does not reference a vertex."
                    );
        }

        Name = name;
        Topology = topology;
        _vertices = vertices.ToArray();
        _indices = indices?.ToArray() ?? [];
        _indicesView = Array.AsReadOnly(_indices);

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

        foreach (var index in _indices)
            hash.Add(index);

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
