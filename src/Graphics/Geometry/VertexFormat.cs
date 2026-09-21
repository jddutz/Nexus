namespace Nexus.Graphics.Geometry;

/// <summary>Describes the attributes and formats used to pack a vertex buffer.</summary>
public sealed class VertexFormat
{
    /// <summary>Gets the vertex attributes in their buffer order.</summary>
    public VertexSemanticEnum[] Inputs { get; }

    /// <summary>Gets the format used to pack position attributes.</summary>
    public VectorFormatEnum PositionFormat { get; }

    /// <summary>Gets the format used to pack color attributes.</summary>
    public ColorFormatEnum ColorFormat { get; }

    /// <summary>Gets the number of bytes occupied by one packed vertex.</summary>
    public uint Stride { get; }

    /// <summary>Gets the identity of this vertex buffer layout.</summary>
    public GraphicsId Id { get; }

    /// <summary>Initializes a vertex buffer layout.</summary>
    /// <param name="inputs">The vertex attributes in their buffer order.</param>
    /// <param name="positionFormat">The format used to pack position attributes.</param>
    /// <param name="colorFormat">The format used to pack color attributes.</param>
    public VertexFormat(
        VertexSemanticEnum[] inputs,
        VectorFormatEnum positionFormat = VectorFormatEnum.Float3D,
        ColorFormatEnum colorFormat = ColorFormatEnum.RGBA8UNorm
    )
    {
        ArgumentNullException.ThrowIfNull(inputs);

        Inputs = [.. inputs];
        PositionFormat = positionFormat;
        ColorFormat = colorFormat;
        Stride = (uint)
            Inputs.Sum(input =>
                input switch
                {
                    VertexSemanticEnum.Position => positionFormat == VectorFormatEnum.Float2D
                        ? sizeof(float) * 2
                        : sizeof(float) * 3,
                    VertexSemanticEnum.Normal => sizeof(float) * 3,
                    VertexSemanticEnum.Color => colorFormat.GetBytesPerPixel(),
                    VertexSemanticEnum.TexCoord => sizeof(float) * 2,
                    _ => throw new ArgumentOutOfRangeException(nameof(inputs), input, null),
                }
            );

        var hash = new IdentityHashBuilder(nameof(VertexFormat));
        foreach (var input in Inputs)
        {
            hash.Add((uint)input);
        }

        Id = hash.Add((uint)PositionFormat).Add((uint)ColorFormat).Compute();
    }
}
