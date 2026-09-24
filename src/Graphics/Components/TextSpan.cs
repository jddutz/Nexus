namespace Nexus.Graphics.Components;

/// <summary>Renders a group of glyphs from one texture atlas as textured-quad instances.</summary>
public sealed class TextSpan : IDrawable, IMeshInstance
{
    private static readonly int InstanceDataSize =
        System.Runtime.CompilerServices.Unsafe.SizeOf<Matrix4X4<float>>()
        + System.Runtime.CompilerServices.Unsafe.SizeOf<Vector4D<float>>()
        + Marshal.SizeOf<Color>();

    private static ulong _nextId;
    private readonly DrawableId _id = new(Interlocked.Increment(ref _nextId));

    /// <summary>Initializes a text span with one or more glyph instances.</summary>
    /// <param name="texture">The texture atlas sampled by every glyph.</param>
    /// <param name="glyphs">The independent glyph instances rendered by the span.</param>
    public TextSpan(ITexture texture, IEnumerable<TextGlyph> glyphs)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(glyphs);

        Texture = texture;
        Glyphs = glyphs.ToArray();
    }

    /// <summary>Gets or sets the mask of render layers in which this span participates.</summary>
    public ulong RenderLayerMask { get; set; } = ulong.MaxValue;

    /// <summary>Gets the glyph instances rendered by this span.</summary>
    public IReadOnlyList<TextGlyph> Glyphs { get; }

    /// <summary>Gets the shared textured-quad mesh geometry used by the glyphs.</summary>
    public Mesh Mesh { get; } = BuiltInMesh.TexturedQuadOffset;

    /// <summary>Gets the texture atlas sampled by the glyphs.</summary>
    public ITexture Texture { get; }

    /// <summary>Gets the sampling behavior used when sampling the texture atlas.</summary>
    public ISamplingBehavior SamplingBehavior { get; set; } = SamplingBehaviors.Smooth;

    /// <summary>Gets the textured-quad vertex shader contract.</summary>
    public VertexShader VertexShader => BuiltInShaders.TexturedQuadVertexShader;

    /// <summary>Gets the tessellation-control shader contract, which is not used.</summary>
    public IShaderContract? TessellationControlShader => null;

    /// <summary>Gets the tessellation-evaluation shader contract, which is not used.</summary>
    public IShaderContract? TessellationEvalShader => null;

    /// <summary>Gets the geometry shader contract, which is not used.</summary>
    public IShaderContract? GeometryShader => null;

    /// <summary>Gets the textured-quad fragment shader contract.</summary>
    public FragmentShader FragmentShader => BuiltInShaders.TexturedQuadFragmentShader;

    /// <summary>Gets the stable identifier of this span.</summary>
    DrawableId IDrawable.Id => _id;

    /// <summary>Gets the number of glyph instances in this span.</summary>
    ulong IDrawable.InstanceCount => checked((ulong)Glyphs.Count);

    /// <summary>Gets the first glyph transform for the mesh-instance compatibility contract.</summary>
    public Matrix4X4<float> TransformationMatrix =>
        Glyphs.Count == 0 ? Matrix4X4<float>.Identity : Glyphs[0].TransformationMatrix;

    /// <summary>Gets the first glyph tint for the mesh-instance compatibility contract.</summary>
    public Color Color => Glyphs.Count == 0 ? Colors.White : Glyphs[0].Color;

    /// <summary>Gets the packed uniform data required by the textured-quad shader.</summary>
    /// <param name="layout">The requested uniform layout.</param>
    /// <returns>An identity view matrix.</returns>
    public ReadOnlyMemory<byte> GetUniformData(ShaderInput[] layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (layout.Length != 1 || layout[0] is not { Semantic: InputSemantics.View, Size: 64 })
            throw new ArgumentException(
                "The uniform layout must contain one 64-byte View input.",
                nameof(layout)
            );

        var data = new byte[64];
        var view = Matrix4X4<float>.Identity;
        MemoryMarshal.Write(data.AsSpan(), in view);
        return data;
    }

    /// <summary>Gets the packed instance data for every glyph in this span.</summary>
    /// <param name="layout">The instance layout required by the vertex shader.</param>
    /// <returns>One packed record for each glyph.</returns>
    public ReadOnlyMemory<byte> GetInstanceData(ShaderInput[] layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (layout.Sum(input => input.Size) != InstanceDataSize)
            throw new ArgumentException(
                "The instance layout stride does not match the text glyph data.",
                nameof(layout)
            );

        var data = new byte[checked(Glyphs.Count * InstanceDataSize)];
        var textureRegionOffset = System.Runtime.CompilerServices.Unsafe.SizeOf<Matrix4X4<float>>();
        var colorOffset =
            textureRegionOffset + System.Runtime.CompilerServices.Unsafe.SizeOf<Vector4D<float>>();
        for (var index = 0; index < Glyphs.Count; index++)
        {
            var destination = data.AsSpan(index * InstanceDataSize, InstanceDataSize);
            var glyph = Glyphs[index];
            var transformationMatrix = glyph.TransformationMatrix;
            var textureRegion = glyph.TextureRegion;
            var color = glyph.Color;
            MemoryMarshal.Write(destination, in transformationMatrix);
            MemoryMarshal.Write(destination[textureRegionOffset..], in textureRegion);
            MemoryMarshal.Write(destination[colorOffset..], in color);
        }

        return data;
    }

    /// <inheritdoc/>
    ulong IDrawable.RenderLayerMask => RenderLayerMask;
}
