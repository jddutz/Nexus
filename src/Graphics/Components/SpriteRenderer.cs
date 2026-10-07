namespace Nexus.Graphics.Components;

/// <summary>Draws a texture using one <see cref="SpriteDrawable"/> drawable.</summary>
public partial class SpriteRenderer : Component, IRenderer
{
    private SpriteDrawable? _drawable;
    private IReadOnlyList<IDrawable> _drawables = [];

    /// <summary>Gets or sets whether this renderer submits its drawable for rendering.</summary>
    [Observable(PublicSetter = true)]
    private bool _isVisible = true;

    /// <inheritdoc />
    public event EventHandler<DrawableEventArgs>? DrawableAdded;

    /// <inheritdoc />
    public event EventHandler<DrawableEventArgs>? DrawableRemoved;

    /// <inheritdoc />
    public IReadOnlyList<IDrawable> Drawables => _drawables;

    [Observable(PublicSetter = true)]
    private ulong _renderLayerMask = RenderLayers.All;

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

    private readonly Dictionary<SpriteInstanceId, SpriteInstance> _instances = new();
    private IGameObject2D? _spatialOwner;

    /// <summary>Gets the stable IDs and editable sprite instances.</summary>
    public IReadOnlyDictionary<SpriteInstanceId, SpriteInstance> Instances =>
        new System.Collections.ObjectModel.ReadOnlyDictionary<SpriteInstanceId, SpriteInstance>(
            _instances
        );

    /// <summary>Adds a sprite and observes subsequent edits.</summary>
    public SpriteInstanceId Add(SpriteInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var id = SpriteInstanceId.New();
        if (!_instances.Values.Any(existing => ReferenceEquals(existing, instance)))
            instance.PropertyChanged += OnInstanceChanged;
        _instances.Add(id, instance);
        SynchronizeDrawable();
        return id;
    }

    /// <summary>Removes a sprite without changing other IDs.</summary>
    public bool Remove(SpriteInstanceId id)
    {
        if (!_instances.Remove(id, out var instance))
            return false;
        if (!_instances.Values.Any(existing => ReferenceEquals(existing, instance)))
            instance.PropertyChanged -= OnInstanceChanged;
        SynchronizeDrawable();
        return true;
    }

    /// <summary>Removes all sprites and their subscriptions.</summary>
    public void Clear()
    {
        foreach (var instance in _instances.Values)
            instance.PropertyChanged -= OnInstanceChanged;
        _instances.Clear();
        SynchronizeDrawable();
    }

    private void OnInstanceChanged(string propertyName) => SynchronizeDrawable();

    protected virtual partial void AfterIsVisibleChanges() => SynchronizeDrawable();

    /// <inheritdoc />
    protected override void OnOwnerChanged()
    {
        if (_spatialOwner is not null)
            _spatialOwner.WorldTransformChanged -= OnWorldTransformChanged;
        _spatialOwner = Owner as IGameObject2D;
        if (_spatialOwner is not null)
            _spatialOwner.WorldTransformChanged += OnWorldTransformChanged;
        base.OnOwnerChanged();
        SynchronizeDrawable();
    }

    private void OnWorldTransformChanged(Matrix4X4<float> previous, Matrix4X4<float> value) =>
        SynchronizeDrawable();

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
            var texture = Texture!;
            var samplingBehavior = SamplingBehavior!;
            RegisterDrawable(
                new SpriteDrawable
                {
                    Texture = texture,
                    RenderLayerMask = RenderLayerMask,
                    DrawOrder = DrawOrder,
                    SamplingBehavior = samplingBehavior,
                    VertexShader = VertexShader,
                    FragmentShader = FragmentShader,
                }
            );
            return;
        }
        _drawable!.SetInstances(
            _instances.Values.Where(instance => instance.IsVisible).ToArray(),
            _spatialOwner?.WorldTransform ?? Matrix4X4<float>.Identity
        );
    }

    /// <summary>Validates state and updates one property on the current drawable.</summary>
    /// <param name="update">The drawable update to apply.</param>
    private void UpdateDrawable(Action<SpriteDrawable> update)
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
        && IsVisible
        && _instances.Values.Any(instance => instance.IsVisible);

    /// <summary>Registers a newly valid drawable with this component.</summary>
    /// <param name="drawable">The drawable to expose.</param>
    private void RegisterDrawable(SpriteDrawable drawable)
    {
        drawable.SetInstances(
            _instances.Values.Where(instance => instance.IsVisible).ToArray(),
            _spatialOwner?.WorldTransform ?? Matrix4X4<float>.Identity
        );
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
        _drawables = [];
        DrawableRemoved?.Invoke(this, new DrawableEventArgs(drawable));
    }
}
