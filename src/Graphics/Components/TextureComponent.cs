namespace Nexus.Graphics.Components;

/// <summary>Draws one texture region over an explicitly sized rectangular destination.</summary>
public partial class TextureComponent : Component, IGraphicsComponent
{
    private readonly TextureDrawable _drawable;
    private static readonly int InstanceDataSize =
        System.Runtime.CompilerServices.Unsafe.SizeOf<Matrix4X4<float>>()
        + System.Runtime.CompilerServices.Unsafe.SizeOf<Vector4D<float>>()
        + Marshal.SizeOf<Color>();

    /// <inheritdoc />
    public override string DisplayName => "Texture";

    [Observable(PublicSetter = true)]
    private ulong _renderLayerMask = ulong.MaxValue;

    [Observable(PublicSetter = true)]
    private Rectangle<float> _bounds = new(0f, 0f, 0f, 0f);

    [Observable(PublicSetter = true, GenerateChangedEvent = false)]
    private Texture? _texture;

    private Rectangle<float> _destination = new(0f, 0f, 1f, 1f);

    [Observable(PublicSetter = true)]
    private Vector4D<float> _texCoord = new(0f, 0f, 1f, 1f);

    [Observable(PublicSetter = true)]
    private Color _color = Colors.White;

    [Observable]
    private Matrix4X4<float> _view = Matrix4X4<float>.Identity;

    [Observable(PublicSetter = true)]
    private ISamplingBehavior _samplingBehavior = SamplingBehaviors.Smooth;

    /// <summary>Initializes a texture component with a corner-pivoted quad by default.</summary>
    /// <param name="centered">Whether the quad is centered on its origin.</param>
    public TextureComponent(bool centered = false)
    {
        IsCentered = centered;
        Mesh = centered ? BuiltInMesh.TexturedQuadCentered : BuiltInMesh.TexturedQuadOffset;
        _drawable = new TextureDrawable(this);
    }

    /// <summary>Gets the mesh used by the drawable contributions.</summary>
    public Mesh Mesh { get; }

    /// <summary>Gets whether the shared quad is centered on its origin.</summary>
    protected bool IsCentered { get; }

    /// <summary>Gets or sets the explicit visual destination rectangle.</summary>
    public Rectangle<float> Destination
    {
        get => _destination;
        set
        {
            ValidateDestinationValue(value);
            if (_destination == value)
                return;

            _destination = value;
            OnPropertyChanged(nameof(Destination));
            InstanceDataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets the number of instance records produced by this component.</summary>
    public int InstanceCount => GetInstanceCount();

    /// <inheritdoc />
    public IReadOnlyList<IDrawable> Drawables => [_drawable];

    event EventHandler<DrawableEventArgs>? IGraphicsComponent.DrawableAdded
    {
        add { }
        remove { }
    }

    event EventHandler<DrawableEventArgs>? IGraphicsComponent.DrawableRemoved
    {
        add { }
        remove { }
    }

    /// <summary>Occurs when the owned drawable's render-layer mask changes.</summary>
    public event EventHandler? RenderLayerChanged;

    /// <summary>Occurs when the owned drawable's texture changes.</summary>
    public event EventHandler? TextureChanged;

    /// <summary>Occurs when the owned drawable's instance data changes.</summary>
    public event EventHandler? InstanceDataChanged;

    /// <summary>Occurs when the owned drawable's uniform data changes.</summary>
    public event EventHandler? UniformDataChanged;

    private void BeforeTextureChanges(Texture? value) => ValidateTextureInput(value, TexCoord);

    private void BeforeSamplingBehaviorChanges(ISamplingBehavior value) =>
        ArgumentNullException.ThrowIfNull(value);

    private void BeforeTexCoordChanges(Vector4D<float> value) =>
        ValidateTextureInput(Texture, value);

    protected virtual partial void AfterRenderLayerMaskChanges(ulong previousValue) =>
        RenderLayerChanged?.Invoke(this, EventArgs.Empty);

    protected virtual partial void AfterTextureChanges(Texture? previousValue) =>
        TextureChanged?.Invoke(this, EventArgs.Empty);

    protected virtual partial void AfterSamplingBehaviorChanges(ISamplingBehavior previousValue) =>
        TextureChanged?.Invoke(this, EventArgs.Empty);

    private void AfterDestinationChanges(Rectangle<float> previousValue) =>
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);

    protected virtual partial void AfterTexCoordChanges(Vector4D<float> previousValue) =>
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);

    protected virtual partial void AfterColorChanges(Color previousValue) =>
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);

    protected virtual partial void AfterViewChanges(Matrix4X4<float> previousValue) =>
        UniformDataChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Gets the instance count for the concrete component.</summary>
    /// <returns>The number of packed records.</returns>
    protected virtual int GetInstanceCount() => 1;

    /// <summary>Gets the local transform and UV rectangle for an instance.</summary>
    /// <param name="instanceIndex">The zero-based instance index.</param>
    /// <param name="transform">The instance-local transform.</param>
    /// <param name="texCoord">The instance UV origin and extent.</param>
    protected virtual void GetInstance(
        int instanceIndex,
        out Matrix4X4<float> transform,
        out Vector4D<float> texCoord
    )
    {
        if (instanceIndex != 0)
            throw new ArgumentOutOfRangeException(nameof(instanceIndex));

        transform = CreateRectangleTransform(
            0f,
            0f,
            Destination.Size.X,
            Destination.Size.Y,
            IsCentered
        );
        texCoord = InsetTexCoord(
            TexCoord,
            insetLeft: true,
            insetTop: true,
            insetRight: true,
            insetBottom: true
        );
    }

    /// <summary>Insets selected outer UV edges to texel centers, leaving internal split edges unchanged.</summary>
    /// <param name="texCoord">The source UV rectangle.</param>
    /// <param name="insetLeft">Whether to inset its left edge.</param>
    /// <param name="insetTop">Whether to inset its top edge.</param>
    /// <param name="insetRight">Whether to inset its right edge.</param>
    /// <param name="insetBottom">Whether to inset its bottom edge.</param>
    /// <returns>The source UV rectangle with selected edges inset by at most half a texel.</returns>
    protected Vector4D<float> InsetTexCoord(
        Vector4D<float> texCoord,
        bool insetLeft,
        bool insetTop,
        bool insetRight,
        bool insetBottom
    )
    {
        if (Texture is null || Texture.Width == 0 || Texture.Height == 0)
            return texCoord;

        var leftInset = insetLeft ? MathF.Min(texCoord.Z, 0.5f / Texture.Width) : 0f;
        var topInset = insetTop ? MathF.Min(texCoord.W, 0.5f / Texture.Height) : 0f;
        var rightInset = insetRight ? MathF.Min(texCoord.Z - leftInset, 0.5f / Texture.Width) : 0f;
        var bottomInset = insetBottom
            ? MathF.Min(texCoord.W - topInset, 0.5f / Texture.Height)
            : 0f;
        return new(
            texCoord.X + leftInset,
            texCoord.Y + topInset,
            texCoord.Z - leftInset - rightInset,
            texCoord.W - topInset - bottomInset
        );
    }

    /// <summary>Validates a proposed texture and source region.</summary>
    /// <param name="texture">The proposed texture.</param>
    /// <param name="texCoord">The proposed source UV rectangle.</param>
    protected virtual void ValidateTextureInput(Texture? texture, Vector4D<float> texCoord) { }

    /// <summary>Validates proposed source border widths.</summary>
    /// <param name="sourceBorders">The proposed source borders.</param>
    protected virtual void ValidateSourceBordersValue(Vector4D<float> sourceBorders) { }

    /// <summary>Raises the instance-data event after derived instance properties change.</summary>
    protected void NotifyInstanceDataChanged() =>
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Creates a transform for a rectangle within the component destination.</summary>
    /// <param name="x">The local left edge.</param>
    /// <param name="y">The local top edge.</param>
    /// <param name="width">The rectangle width.</param>
    /// <param name="height">The rectangle height.</param>
    /// <param name="centered">Whether the quad mesh is centered on its origin.</param>
    /// <returns>The composed instance transform.</returns>
    protected Matrix4X4<float> CreateRectangleTransform(
        float x,
        float y,
        float width,
        float height,
        bool centered
    )
    {
        var offsetX = Destination.Origin.X + x + (centered ? width / 2f : 0f);
        var offsetY = Destination.Origin.Y + y + (centered ? height / 2f : 0f);
        return Matrix4X4.CreateScale(width, height, 1f)
            * Matrix4X4.CreateTranslation(offsetX, offsetY, 0f);
    }

    /// <summary>Validates a finite destination origin and positive finite extents.</summary>
    /// <param name="destination">The requested visual destination.</param>
    private static void ValidateDestinationValue(Rectangle<float> destination)
    {
        if (
            !float.IsFinite(destination.Origin.X)
            || !float.IsFinite(destination.Origin.Y)
            || !float.IsFinite(destination.Size.X)
            || !float.IsFinite(destination.Size.Y)
            || destination.Size.X <= 0f
            || destination.Size.Y <= 0f
        )
            throw new ArgumentOutOfRangeException(
                nameof(destination),
                "Destination origin must be finite and both extents must be positive and finite."
            );
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetUniformData(ShaderInput[] layout)
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

    /// <summary>Gets or writes one packed transform, UV rectangle, and tint record.</summary>
    /// <param name="instanceIndex">The zero-based instance index.</param>
    /// <param name="destination">The destination span, or an empty span when querying its size.</param>
    /// <returns>The required or written byte count.</returns>
    public int GetInstanceData(int instanceIndex, Span<byte> destination)
    {
        if ((uint)instanceIndex >= (uint)InstanceCount)
            throw new ArgumentOutOfRangeException(nameof(instanceIndex));
        if (destination.Length < InstanceDataSize)
            return InstanceDataSize;

        GetInstance(instanceIndex, out var transform, out var texCoord);
        var transformSize = System.Runtime.CompilerServices.Unsafe.SizeOf<Matrix4X4<float>>();
        var texCoordSize = System.Runtime.CompilerServices.Unsafe.SizeOf<Vector4D<float>>();
        MemoryMarshal.Write(destination, in transform);
        MemoryMarshal.Write(destination[transformSize..], in texCoord);
        MemoryMarshal.Write(destination[(transformSize + texCoordSize)..], in _color);
        return InstanceDataSize;
    }

    /// <summary>Gets the packed instance data for every instance in this component.</summary>
    /// <param name="layout">The instance layout required by the vertex shader.</param>
    /// <returns>One packed record for each instance.</returns>
    private sealed class TextureDrawable : IDrawable
    {
        private readonly TextureComponent _owner;

        public TextureDrawable(TextureComponent owner) => _owner = owner;

        public DrawableId Id { get; } = DrawableId.New();
        public ulong RenderLayerMask => _owner.RenderLayerMask;
        public Mesh Mesh => _owner.Mesh;
        public ITexture Texture =>
            _owner.Texture ?? global::Nexus.Graphics.Textures.Texture.Invalid;
        public ulong InstanceCount => checked((ulong)_owner.InstanceCount);
        public ISamplingBehavior SamplingBehavior => _owner.SamplingBehavior;
        public VertexShader? VertexShader => BuiltInShaders.TexturedQuadVertexShader;
        public IShaderContract? TessellationControlShader => null;
        public IShaderContract? TessellationEvalShader => null;
        public IShaderContract? GeometryShader => null;
        public FragmentShader? FragmentShader => BuiltInShaders.TexturedQuadFragmentShader;
        public event EventHandler? RenderLayerChanged
        {
            add => _owner.RenderLayerChanged += value;
            remove => _owner.RenderLayerChanged -= value;
        }
        public event EventHandler? MeshChanged
        {
            add { }
            remove { }
        }
        public event EventHandler? TextureChanged
        {
            add => _owner.TextureChanged += value;
            remove => _owner.TextureChanged -= value;
        }
        public event EventHandler? InstanceDataChanged
        {
            add => _owner.InstanceDataChanged += value;
            remove => _owner.InstanceDataChanged -= value;
        }
        public event EventHandler? UniformDataChanged
        {
            add => _owner.UniformDataChanged += value;
            remove => _owner.UniformDataChanged -= value;
        }
        public event EventHandler? ShaderChanged
        {
            add { }
            remove { }
        }

        public ReadOnlyMemory<byte> GetUniformData(ShaderInput[] layout) =>
            _owner.GetUniformData(layout);

        public ReadOnlyMemory<byte> GetInstanceData(ShaderInput[] layout)
        {
            ArgumentNullException.ThrowIfNull(layout);
            if (layout.Sum(input => input.Size) != InstanceDataSize)
                throw new ArgumentException(
                    "The instance layout stride does not match the textured component data.",
                    nameof(layout)
                );

            var data = new byte[checked(_owner.InstanceCount * InstanceDataSize)];
            for (var index = 0; index < _owner.InstanceCount; index++)
                _owner.GetInstanceData(
                    index,
                    data.AsSpan(index * InstanceDataSize, InstanceDataSize)
                );

            return data;
        }
    }
}
