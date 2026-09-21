namespace Nexus.Graphics.Components;

/// <summary>
/// Renders geometry with a per-instance transformation matrix and uniform color.
/// </summary>
public class UniformColorMeshRenderer()
    : Component,
        IGraphicsComponent,
        IRenderable,
        IInstanceDataSource,
        IMeshInstance
{
    private static readonly int InstanceDataSize =
        System.Runtime.CompilerServices.Unsafe.SizeOf<Matrix4X4<float>>() + Marshal.SizeOf<Color>();

    private HashSet<RenderLayer> _renderLayers = [];
    private Mesh _mesh = BuiltInMesh.Empty;
    private Matrix4X4<float> _transformationMatrix = Matrix4X4<float>.Identity;
    private Color _color = Colors.Black;
    private Matrix4X4<float> _view = Matrix4X4<float>.Identity;

    /// <summary>
    /// Gets the render layers in which this component participates.
    /// </summary>
    public IEnumerable<RenderLayer> RenderLayers => _renderLayers;

    /// <summary>
    /// Gets the single renderable contribution produced by this component.
    /// </summary>
    public IReadOnlyList<IRenderable> Renderables => [this];

    /// <inheritdoc/>
    GraphicsId IRenderable.Id => Id;

    IVertexDataSource IRenderable.Vertices => Mesh.Source;

    ITexture IRenderable.Texture => global::Nexus.Graphics.Textures.Texture.Uniform;

    IInstanceDataSource IRenderable.Instances => this;

    GraphicsId IInstanceDataSource.Id =>
        new IdentityHashBuilder(nameof(UniformColorMeshRenderer)).Add(Id).Compute();

    ulong IInstanceDataSource.Count => 1;

    VertexShader? IRenderable.VertexShader => BuiltInShaders.UniformColorVertexShader;

    IShaderContract? IRenderable.TessellationControlShader => null;

    IShaderContract? IRenderable.TessellationEvalShader => null;

    IShaderContract? IRenderable.GeometryShader => null;

    FragmentShader? IRenderable.FragmentShader => BuiltInShaders.UniformColorFragmentShader;

    IShaderContract? IRenderable.ComputeShader => null;

    /// <summary>Gets the number of packed instance records contributed by this component.</summary>
    public int InstanceCount => 1;

    /// <summary>
    /// Gets or sets the mesh rendered by this component.
    /// </summary>
    public Mesh Mesh
    {
        get => _mesh;
        set => SetProperty(ref _mesh, value);
    }

    /// <summary>
    /// Gets or sets the transformation matrix applied to the geometry.
    /// </summary>
    public Matrix4X4<float> TransformationMatrix
    {
        get => _transformationMatrix;
        set => SetProperty(ref _transformationMatrix, value);
    }

    /// <summary>
    /// Gets or sets the color applied to the rendered geometry.
    /// </summary>
    public Color Color
    {
        get => _color;
        set => SetProperty(ref _color, value);
    }

    /// <summary>Gets or sets the view matrix supplied to the vertex shader contract.</summary>
    public Matrix4X4<float> View
    {
        get => _view;
        set => SetProperty(ref _view, value);
    }

    ReadOnlyMemory<byte> IRenderable.GetUniformData(ShaderInput[] layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (layout.Length != 1 || layout[0] is not { Semantic: InputSemantics.View, Size: 64 })
            throw new ArgumentException(
                "The uniform layout must contain one 64-byte View input.",
                nameof(layout)
            );

        var data = new byte[64];
        MemoryMarshal.Write(data.AsSpan(), in _view);
        return data;
    }

    /// <summary>
    /// Gets the required instance-record size or writes the transform and color to a destination span.
    /// </summary>
    /// <param name="destination">The destination for the instance record, or an empty span when querying its size.</param>
    /// <returns>The required or written byte count.</returns>
    public int GetInstanceData(Span<byte> destination)
    {
        var transformationMatrixSize = System.Runtime.CompilerServices.Unsafe.SizeOf<
            Matrix4X4<float>
        >();

        if (destination.Length < InstanceDataSize)
            return InstanceDataSize;

        var transformationMatrix = TransformationMatrix;
        var color = Color;
        MemoryMarshal.Write(destination, in transformationMatrix);
        MemoryMarshal.Write(destination[transformationMatrixSize..], in color);

        return InstanceDataSize;
    }

    /// <summary>
    /// Gets or writes the packed instance record at the specified component-local index.
    /// </summary>
    /// <param name="instanceIndex">The zero-based instance index.</param>
    /// <param name="destination">The destination span, or an empty span when querying its size.</param>
    /// <returns>The required or written byte count.</returns>
    public int GetInstanceData(int instanceIndex, Span<byte> destination)
    {
        if (instanceIndex != 0)
            throw new ArgumentOutOfRangeException(nameof(instanceIndex));

        return GetInstanceData(destination);
    }

    ReadOnlyMemory<byte> IInstanceDataSource.GetInstanceData(ShaderInput[] layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (layout.Sum(input => input.Size) != InstanceDataSize)
            throw new ArgumentException(
                "The instance layout stride does not match the renderer data.",
                nameof(layout)
            );

        var data = new byte[InstanceDataSize];
        GetInstanceData(data);
        return data;
    }
}
