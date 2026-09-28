namespace Nexus.Graphics.Components;

/// <summary>
/// Renders geometry with a per-instance transformation matrix and uniform color.
/// </summary>
public class UniformColorMeshRenderer() : Component, IGraphicsComponent, IDrawable, IMeshInstance
{
    private static readonly int InstanceDataSize =
        System.Runtime.CompilerServices.Unsafe.SizeOf<Matrix4X4<float>>() + Marshal.SizeOf<Color>();

    private ulong _renderLayerMask = 1;
    private Mesh _mesh = BuiltInMesh.Empty;
    private Matrix4X4<float> _transformationMatrix = Matrix4X4<float>.Identity;
    private Color _color = Colors.Black;
    private Matrix4X4<float> _view = Matrix4X4<float>.Identity;
    private ISamplingBehavior _samplingBehavior = SamplingBehaviors.Smooth;

    /// <inheritdoc />
    public override string DisplayName => "Uniform Color Mesh";

    /// <summary>
    /// Gets or sets the mask of render layers in which this component participates.
    /// </summary>
    public ulong RenderLayerMask
    {
        get => _renderLayerMask;
        set
        {
            if (SetProperty(ref _renderLayerMask, value))
                RenderLayerChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets the single renderable contribution produced by this component.
    /// </summary>
    public IReadOnlyList<IDrawable> Drawables => [this];

    /// <inheritdoc/>
    public event EventHandler<DrawableEventArgs>? DrawableAdded;

    /// <inheritdoc/>
    public event EventHandler<DrawableEventArgs>? DrawableRemoved;

    /// <inheritdoc/>
    public event EventHandler? RenderLayerChanged;

    /// <inheritdoc/>
    public event EventHandler? MeshChanged;

    /// <inheritdoc/>
    public event EventHandler? TextureChanged;

    /// <inheritdoc/>
    public event EventHandler? InstanceDataChanged;

    /// <inheritdoc/>
    public event EventHandler? UniformDataChanged;

    /// <inheritdoc/>
    event EventHandler? IDrawable.ShaderChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    DrawableId IDrawable.Id => new(Id.Value);

    /// <inheritdoc/>
    ulong IDrawable.RenderLayerMask => RenderLayerMask;

    Mesh IDrawable.Mesh => Mesh;

    ITexture IDrawable.Texture => global::Nexus.Graphics.Textures.Texture.Uniform;

    ISamplingBehavior IDrawable.SamplingBehavior => SamplingBehavior;

    ulong IDrawable.InstanceCount => checked((ulong)InstanceCount);

    VertexShader? IDrawable.VertexShader => BuiltInShaders.UniformColorVertexShader;

    IShaderContract? IDrawable.TessellationControlShader => null;

    IShaderContract? IDrawable.TessellationEvalShader => null;

    IShaderContract? IDrawable.GeometryShader => null;

    FragmentShader? IDrawable.FragmentShader => BuiltInShaders.UniformColorFragmentShader;

    /// <summary>Gets the number of packed instance records contributed by this component.</summary>
    public int InstanceCount => 1;

    /// <summary>
    /// Gets or sets the mesh rendered by this component.
    /// </summary>
    public Mesh Mesh
    {
        get => _mesh;
        set
        {
            if (SetProperty(ref _mesh, value))
                MeshChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets or sets the sampling behavior associated with this renderable.
    /// </summary>
    public ISamplingBehavior SamplingBehavior
    {
        get => _samplingBehavior;
        set
        {
            if (SetProperty(ref _samplingBehavior, value))
                TextureChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets or sets the transformation matrix applied to the geometry.
    /// </summary>
    public Matrix4X4<float> TransformationMatrix
    {
        get => _transformationMatrix;
        set
        {
            if (SetProperty(ref _transformationMatrix, value))
                InstanceDataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets or sets the color applied to the rendered geometry.
    /// </summary>
    public Color Color
    {
        get => _color;
        set
        {
            if (SetProperty(ref _color, value))
                InstanceDataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the view matrix supplied to the vertex shader contract.</summary>
    public Matrix4X4<float> View
    {
        get => _view;
        set
        {
            if (SetProperty(ref _view, value))
                UniformDataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    ReadOnlyMemory<byte> IDrawable.GetUniformData(ShaderInput[] layout)
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

    ReadOnlyMemory<byte> IDrawable.GetInstanceData(ShaderInput[] layout)
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
