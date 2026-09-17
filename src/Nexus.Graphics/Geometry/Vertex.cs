namespace Nexus.Graphics.Geometry;

public readonly struct Vertex(
    Vector3D<float> position,
    Vector3D<float>? normal = null,
    Color? color = null,
    Vector2D<float>? texCoord = null
)
{
    public Vector3D<float> Position { get; } = position;
    public Vector3D<float> Normal { get; } = normal ?? new(0.0f, 0.0f, 1.0f); // Is Z-positive axis the correct default for 2D objects?
    public Color Color { get; } = color ?? Colors.White;
    public Vector2D<float> TexCoord { get; } = texCoord ?? new(0.0f, 0.0f);

    /// <summary>Writes this vertex's selected attributes into the destination buffer.</summary>
    /// <param name="destination">The destination buffer to receive the packed vertex data.</param>
    /// <param name="inputs">The vertex attributes to write, in buffer order.</param>
    /// <param name="positionFormat">The position format, defaulting to <see cref="VectorFormatEnum.Float3D"/>.</param>
    /// <param name="colorFormat">The color format, defaulting to <see cref="ColorFormatEnum.RGBA8UNorm"/>.</param>
    /// <returns>The number of bytes written.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="destination"/> is too small.</exception>
    public int WriteVertexData(
        Span<byte> destination,
        VertexSemanticEnum[] inputs,
        VectorFormatEnum? positionFormat = null,
        ColorFormatEnum? colorFormat = null
    )
    {
        var resolvedPositionFormat = positionFormat ?? VectorFormatEnum.Float3D;

        var resolvedColorFormat = colorFormat ?? ColorFormatEnum.RGBA8UNorm;

        var size = GetVertexDataSize(inputs, resolvedPositionFormat, resolvedColorFormat);
        if (destination.Length < size)
        {
            throw new ArgumentException("The destination span is too small.", nameof(destination));
        }

        var offset = 0;

        foreach (var input in inputs)
        {
            offset += input switch
            {
                VertexSemanticEnum.Position => resolvedPositionFormat == VectorFormatEnum.Float2D
                    ? PackVector2D(Position, destination[offset..])
                    : PackVector3D(Position, destination[offset..]),

                VertexSemanticEnum.Normal => PackVector3D(Normal, destination[offset..]),

                VertexSemanticEnum.Color => PackColor(
                    Color,
                    resolvedColorFormat,
                    destination[offset..]
                ),

                VertexSemanticEnum.TexCoord => PackVector2D(TexCoord, destination[offset..]),

                _ => 0,
            };
        }

        return offset;
    }

    /// <summary>Creates packed vertex data for this vertex's selected attributes.</summary>
    /// <param name="inputs">The vertex attributes to write, in buffer order.</param>
    /// <param name="positionFormat">The position format, defaulting to <see cref="VectorFormatEnum.Float3D"/>.</param>
    /// <param name="colorFormat">The color format, defaulting to <see cref="ColorFormatEnum.RGBA8UNorm"/>.</param>
    /// <returns>The packed vertex data.</returns>
    public ReadOnlyMemory<byte> ToVertexData(
        VertexSemanticEnum[] inputs,
        VectorFormatEnum? positionFormat = null,
        ColorFormatEnum? colorFormat = null
    )
    {
        var data = new byte[GetVertexDataSize(inputs, positionFormat, colorFormat)];
        WriteVertexData(data, inputs, positionFormat, colorFormat);
        return data;
    }

    /// <summary>Computes the number of bytes a vertex occupies when packed with the given attributes and formats.</summary>
    /// <param name="inputs">The vertex attributes to include, in buffer order.</param>
    /// <param name="positionFormat">The position format, defaulting to <see cref="VectorFormatEnum.Float3D"/>.</param>
    /// <param name="colorFormat">The color format, defaulting to <see cref="ColorFormatEnum.RGBA8UNorm"/>.</param>
    /// <returns>The stride, in bytes, of one packed vertex.</returns>
    public static int GetVertexDataSize(
        VertexSemanticEnum[] inputs,
        VectorFormatEnum? positionFormat = null,
        ColorFormatEnum? colorFormat = null
    )
    {
        var resolvedPositionFormat = positionFormat ?? VectorFormatEnum.Float3D;

        var resolvedColorFormat = colorFormat ?? ColorFormatEnum.RGBA8UNorm;

        return inputs.Sum(input =>
            input switch
            {
                VertexSemanticEnum.Position => resolvedPositionFormat == VectorFormatEnum.Float2D
                    ? sizeof(float) * 2
                    : sizeof(float) * 3,
                VertexSemanticEnum.Normal => sizeof(float) * 3,
                VertexSemanticEnum.Color => resolvedColorFormat.GetBytesPerPixel(),
                VertexSemanticEnum.TexCoord => sizeof(float) * 2,
                _ => throw new ArgumentOutOfRangeException(nameof(inputs), input, null),
            }
        );
    }

    private static int PackColor(Color value, ColorFormatEnum format, Span<byte> destination) =>
        value.WriteColorData(format, destination);

    private static int PackVector2D(Vector3D<float> value, Span<byte> destination)
    {
        MemoryMarshal.Write(destination, value.X);
        MemoryMarshal.Write(destination[sizeof(float)..], value.Y);

        return sizeof(float) * 2;
    }

    private static int PackVector2D(Vector2D<float> value, Span<byte> destination)
    {
        MemoryMarshal.Write(destination, value.X);
        MemoryMarshal.Write(destination[sizeof(float)..], value.Y);

        return sizeof(float) * 2;
    }

    private static int PackVector3D(Vector3D<float> value, Span<byte> destination)
    {
        MemoryMarshal.Write(destination, value.X);
        MemoryMarshal.Write(destination[sizeof(float)..], value.Y);
        MemoryMarshal.Write(destination[(sizeof(float) * 2)..], value.Z);

        return sizeof(float) * 3;
    }
}
