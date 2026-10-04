namespace Nexus.Graphics.Components;

/// <summary>Draws a resizable texture region as four corners, four edges, and a center.</summary>
public partial class NinePatchRenderer : Component, IGraphicsComponent
{
    private NinePatch? _drawable;
    private IReadOnlyList<IDrawable> _drawables = Array.Empty<IDrawable>();

    /// <inheritdoc />
    public IReadOnlyList<IDrawable> Drawables => _drawables;

    /// <inheritdoc />
    public event EventHandler<DrawableEventArgs>? DrawableAdded;

    /// <inheritdoc />
    public event EventHandler<DrawableEventArgs>? DrawableRemoved;

    [Observable(PublicSetter = true)]
    private ulong _renderLayerMask = ulong.MaxValue;

    /// <inheritdoc />
    protected virtual partial void AfterRenderLayerMaskChanges(ulong previousValue) =>
        UpdateDrawable(drawable => drawable.RenderLayerMask = RenderLayerMask);

    /// <summary>Gets or sets the render order assigned to the drawable.</summary>
    [Observable(PublicSetter = true)]
    private int _drawOrder;

    /// <inheritdoc />
    protected virtual partial void AfterDrawOrderChanges(int previousValue) =>
        UpdateDrawable(drawable => drawable.DrawOrder = DrawOrder);

    [Observable(PublicSetter = true)]
    private ITexture? _texture = null;

    /// <inheritdoc />
    protected virtual partial void AfterTextureChanges() =>
        UpdateDrawable(drawable => drawable.Texture = Texture!);

    [Observable(PublicSetter = true)]
    private ISamplingBehavior? _samplingBehavior = SamplingBehaviors.Smooth;

    /// <inheritdoc />
    protected virtual partial void AfterSamplingBehaviorChanges() =>
        UpdateDrawable(drawable => drawable.SamplingBehavior = SamplingBehavior!);

    [Observable(PublicSetter = true)]
    private VertexShader? _vertexShader = BuiltInShaders.TexturedQuadVertexShader;

    /// <inheritdoc />
    protected virtual partial void AfterVertexShaderChanges(VertexShader? previousValue) =>
        UpdateDrawable(drawable => drawable.VertexShader = VertexShader);

    [Observable(PublicSetter = true)]
    private FragmentShader? _fragmentShader = BuiltInShaders.TexturedQuadFragmentShader;

    /// <inheritdoc />
    protected virtual partial void AfterFragmentShaderChanges(FragmentShader? previousValue) =>
        UpdateDrawable(drawable => drawable.FragmentShader = FragmentShader);

    [Observable(PublicSetter = true)]
    private Rectangle<float> _destination = new(0f, 0f, 1f, 1f);

    /// <inheritdoc />
    protected virtual partial void AfterDestinationChanges() =>
        UpdateDrawable(drawable => drawable.Destination = Destination);

    [Observable(PublicSetter = true)]
    private Vector4D<float> _texCoord = new(0f, 0f, 1f, 1f);

    /// <inheritdoc />
    protected virtual partial void AfterTexCoordChanges() =>
        UpdateDrawable(drawable => drawable.TexCoord = TexCoord);

    [Observable(PublicSetter = true)]
    private Vector4D<float> _sourceBorders = new(0f, 0f, 0f, 0f);

    /// <inheritdoc />
    protected virtual partial void AfterSourceBordersChanges() =>
        UpdateDrawable(drawable => drawable.SourceBorders = SourceBorders);

    [Observable(PublicSetter = true)]
    private Color _color = Colors.White;

    /// <inheritdoc />
    protected virtual partial void AfterColorChanges() =>
        UpdateDrawable(drawable => drawable.Color = Color);

    /// <summary>Validates component state and synchronizes the rendering drawable.</summary>
    private void SynchronizeDrawable()
    {
        if (!IsValidState())
        {
            UnregisterDrawable();
            return;
        }

        if (_drawable is null)
        {
            RegisterDrawable(
                new NinePatch
                {
                    Texture = Texture!,
                    RenderLayerMask = RenderLayerMask,
                    DrawOrder = DrawOrder,
                    SamplingBehavior = SamplingBehavior!,
                    VertexShader = VertexShader,
                    FragmentShader = FragmentShader,
                    Destination = Destination,
                    TexCoord = TexCoord,
                    SourceBorders = SourceBorders,
                    Color = Color,
                }
            );
        }
    }

    /// <summary>Validates state and updates one property on the current drawable.</summary>
    /// <param name="update">The drawable update to apply.</param>
    private void UpdateDrawable(Action<NinePatch> update)
    {
        if (!IsValidState())
        {
            UnregisterDrawable();
            return;
        }

        if (_drawable is null)
        {
            SynchronizeDrawable();
            return;
        }

        update(_drawable);
    }

    /// <summary>Determines whether all component values can produce a drawable.</summary>
    private bool IsValidState() =>
        Texture is not null
        && SamplingBehavior is not null
        && VertexShader is not null
        && FragmentShader is not null
        && IsValidDestination(Destination)
        && IsValidTexCoord(TexCoord)
        && IsValidSourceBorders(SourceBorders)
        && IsValidBordersFit(SourceBorders, Texture, TexCoord)
        && IsValidColor(Color);

    /// <summary>Validates destination coordinates and positive extents.</summary>
    private static bool IsValidDestination(Rectangle<float> destination) =>
        float.IsFinite(destination.Origin.X)
        && float.IsFinite(destination.Origin.Y)
        && float.IsFinite(destination.Size.X)
        && float.IsFinite(destination.Size.Y)
        && destination.Size.X > 0f
        && destination.Size.Y > 0f;

    /// <summary>Validates a normalized texture region.</summary>
    private static bool IsValidTexCoord(Vector4D<float> texCoord) =>
        float.IsFinite(texCoord.X)
        && float.IsFinite(texCoord.Y)
        && float.IsFinite(texCoord.Z)
        && float.IsFinite(texCoord.W)
        && texCoord.X >= 0f
        && texCoord.Y >= 0f
        && texCoord.Z > 0f
        && texCoord.W > 0f
        && texCoord.X + texCoord.Z <= 1f
        && texCoord.Y + texCoord.W <= 1f;

    /// <summary>Validates finite, non-negative source border widths.</summary>
    private static bool IsValidSourceBorders(Vector4D<float> sourceBorders) =>
        float.IsFinite(sourceBorders.X)
        && float.IsFinite(sourceBorders.Y)
        && float.IsFinite(sourceBorders.Z)
        && float.IsFinite(sourceBorders.W)
        && sourceBorders.X >= 0f
        && sourceBorders.Y >= 0f
        && sourceBorders.Z >= 0f
        && sourceBorders.W >= 0f;

    /// <summary>Validates that source borders fit the selected texture region.</summary>
    private static bool IsValidBordersFit(
        Vector4D<float> sourceBorders,
        ITexture? texture,
        Vector4D<float> texCoord
    ) =>
        texture is null
        || (
            sourceBorders.X + sourceBorders.Z <= texCoord.Z * texture.Width
            && sourceBorders.Y + sourceBorders.W <= texCoord.W * texture.Height
        );

    /// <summary>Validates color channels in the normalized color range.</summary>
    private static bool IsValidColor(Color color) =>
        float.IsFinite(color.R)
        && float.IsFinite(color.G)
        && float.IsFinite(color.B)
        && float.IsFinite(color.A)
        && color.R is >= 0f and <= 1f
        && color.G is >= 0f and <= 1f
        && color.B is >= 0f and <= 1f
        && color.A is >= 0f and <= 1f;

    /// <summary>Registers a newly valid drawable with this component.</summary>
    /// <param name="drawable">The drawable to expose.</param>
    private void RegisterDrawable(NinePatch drawable)
    {
        _drawable = drawable;
        _drawables = [drawable];
        DrawableAdded?.Invoke(this, new DrawableEventArgs(drawable));
    }

    /// <summary>Unregisters the current drawable when component state is invalid.</summary>
    private void UnregisterDrawable()
    {
        if (_drawable is null)
            return;

        var drawable = _drawable;
        _drawable = null;
        _drawables = Array.Empty<IDrawable>();
        DrawableRemoved?.Invoke(this, new DrawableEventArgs(drawable));
    }
}
