using System.ComponentModel;

namespace Nexus.Graphics.Components;

/// <summary>Draws one texture region over an explicitly sized rectangular destination.</summary>
public class TextureComponent : Nexus.Core.Component, IGraphicsComponent, IDrawable
{
    private static readonly int InstanceDataSize =
        System.Runtime.CompilerServices.Unsafe.SizeOf<Matrix4X4<float>>()
        + System.Runtime.CompilerServices.Unsafe.SizeOf<Vector4D<float>>()
        + Marshal.SizeOf<Color>();

    private ulong _renderLayerMask = 1;
    private Texture? _texture;
    private ColorFormatEnum _textureFormat = ColorFormatEnum.RGBA8UNorm;
    private Vector2D<float> _size = new(1f, 1f);
    private Matrix4X4<float> _transformationMatrix = Matrix4X4<float>.Identity;
    private Vector4D<float> _texCoord = new(0f, 0f, 1f, 1f);
    private Color _color = Colors.White;
    private Matrix4X4<float> _view = Matrix4X4<float>.Identity;
    private ISamplingBehavior _samplingBehavior = SamplingBehaviors.Smooth;

    /// <summary>Initializes a texture component with a corner-pivoted quad by default.</summary>
    /// <param name="centered">Whether the quad is centered on its origin.</param>
    public TextureComponent(bool centered = false)
    {
        IsCentered = centered;
        Mesh = centered ? BuiltInMesh.TexturedQuadCentered : BuiltInMesh.TexturedQuadOffset;
    }

    /// <inheritdoc />
    /// <inheritdoc />
    public override string DisplayName => "Texture";

    /// <summary>Gets the mesh used by the drawable contributions.</summary>
    public Mesh Mesh { get; }

    /// <summary>Gets whether the shared quad is centered on its origin.</summary>
    protected bool IsCentered { get; }

    /// <summary>Gets the number of instance records produced by this component.</summary>
    public int InstanceCount => GetInstanceCount();

    /// <inheritdoc />
    public IReadOnlyList<IDrawable> Drawables => [this];

    /// <inheritdoc />
    public event EventHandler<DrawableEventArgs>? DrawableAdded;

    /// <inheritdoc />
    public event EventHandler<DrawableEventArgs>? DrawableRemoved;

    /// <inheritdoc />
    DrawableId IDrawable.Id => new(Id.Value);

    /// <inheritdoc />
    ulong IDrawable.RenderLayerMask => RenderLayerMask;

    /// <inheritdoc />
    ITexture IDrawable.Texture => _texture ?? global::Nexus.Graphics.Textures.Texture.Invalid;

    /// <inheritdoc />
    ColorFormatEnum IDrawable.TextureFormat => TextureFormat;

    /// <inheritdoc />
    ulong IDrawable.InstanceCount => checked((ulong)InstanceCount);

    /// <inheritdoc />
    ISamplingBehavior IDrawable.SamplingBehavior => SamplingBehavior;

    /// <inheritdoc />
    VertexShader? IDrawable.VertexShader => BuiltInShaders.TexturedQuadVertexShader;

    /// <inheritdoc />
    IShaderContract? IDrawable.TessellationControlShader => null;

    /// <inheritdoc />
    IShaderContract? IDrawable.TessellationEvalShader => null;

    /// <inheritdoc />
    IShaderContract? IDrawable.GeometryShader => null;

    /// <inheritdoc />
    FragmentShader? IDrawable.FragmentShader => BuiltInShaders.TexturedQuadFragmentShader;

    /// <inheritdoc />
    public event EventHandler? RenderLayerChanged;

    /// <inheritdoc />
    event EventHandler? IDrawable.MeshChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public event EventHandler? TextureChanged;

    /// <inheritdoc />
    public event EventHandler? InstanceDataChanged;

    /// <inheritdoc />
    public event EventHandler? UniformDataChanged;

    /// <inheritdoc />
    event EventHandler? IDrawable.ShaderChanged
    {
        add { }
        remove { }
    }

    /// <summary>Gets or sets the mask of render layers in which this component participates.</summary>
    public ulong RenderLayerMask
    {
        get => _renderLayerMask;
        set
        {
            if (SetProperty(ref _renderLayerMask, value))
                RenderLayerChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the sampled texture.</summary>
    public Texture? Texture
    {
        get => _texture;
        set
        {
            ValidateTextureInput(value, TexCoord);
            if (SetProperty(ref _texture, value))
                TextureChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the storage and sampling format of the texture.</summary>
    public ColorFormatEnum TextureFormat
    {
        get => _textureFormat;
        set
        {
            if (SetProperty(ref _textureFormat, value))
                TextureChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the texture sampling behavior.</summary>
    public ISamplingBehavior SamplingBehavior
    {
        get => _samplingBehavior;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetProperty(ref _samplingBehavior, value))
                TextureChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the destination size in logical units. Both dimensions must be positive.</summary>
    public Vector2D<float> Size
    {
        get => _size;
        set
        {
            ValidateSize(value);
            if (SetProperty(ref _size, value))
                InstanceDataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets an additional local transform applied before the owning object's world transform.</summary>
    public Matrix4X4<float> TransformationMatrix
    {
        get => _transformationMatrix;
        set
        {
            if (SetProperty(ref _transformationMatrix, value))
                InstanceDataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the atlas UV origin and extent.</summary>
    public Vector4D<float> TexCoord
    {
        get => _texCoord;
        set
        {
            ValidateTextureInput(Texture, value);
            if (SetProperty(ref _texCoord, value))
                InstanceDataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the tint multiplied against the sampled texture color.</summary>
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

    /// <inheritdoc />
    protected override void OnOwnerChanged()
    {
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);
        base.OnOwnerChanged();
    }

    /// <inheritdoc />
    protected override void OnOwnerPropertyChanged(PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IGameObject2D.WorldTransform))
            InstanceDataChanged?.Invoke(this, EventArgs.Empty);

        base.OnOwnerPropertyChanged(e);
    }

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

        transform = CreateRectangleTransform(0f, 0f, Size.X, Size.Y, IsCentered);
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
    protected virtual void ValidateSourceBorders(Vector4D<float> sourceBorders) { }

    /// <summary>Raises the instance-data event after derived instance properties change.</summary>
    protected void NotifyInstanceDataChanged() =>
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Composes a rectangle transform with this component and its owner transforms.</summary>
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
        var offsetX = centered ? x + (width - Size.X) / 2f : x;
        var offsetY = centered ? y + (height - Size.Y) / 2f : y;
        var localTransform =
            Matrix4X4.CreateScale(width, height, 1f)
            * Matrix4X4.CreateTranslation(offsetX, offsetY, 0f)
            * TransformationMatrix;
        var ownerTransform = GetOwnerTransformationMatrix();
        return localTransform * ownerTransform;
    }

    /// <summary>Gets the owning 2D object's world transform, or identity when there is no 2D owner.</summary>
    /// <returns>The owner world transform.</returns>
    protected Matrix4X4<float> GetOwnerTransformationMatrix() =>
        GameModel?.GetGameObject(GameObjectId) is IGameObject2D gameObject
            ? gameObject.WorldTransform
            : Matrix4X4<float>.Identity;

    /// <summary>Validates a positive finite destination size.</summary>
    /// <param name="size">The requested destination size.</param>
    private static void ValidateSize(Vector2D<float> size)
    {
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X <= 0f || size.Y <= 0f)
            throw new ArgumentOutOfRangeException(
                nameof(size),
                "Both size dimensions must be positive and finite."
            );
    }

    /// <inheritdoc />
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
    ReadOnlyMemory<byte> IDrawable.GetInstanceData(ShaderInput[] layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (layout.Sum(input => input.Size) != InstanceDataSize)
            throw new ArgumentException(
                "The instance layout stride does not match the textured component data.",
                nameof(layout)
            );

        var data = new byte[checked(InstanceCount * InstanceDataSize)];
        for (var index = 0; index < InstanceCount; index++)
            GetInstanceData(index, data.AsSpan(index * InstanceDataSize, InstanceDataSize));

        return data;
    }
}
