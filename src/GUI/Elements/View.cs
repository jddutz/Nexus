namespace Nexus.GUI.Elements;

using Nexus.Graphics;
using Nexus.Graphics.Cameras;
using Nexus.Graphics.Components;

/// <summary>
/// Defines a rendering viewport whose screen area is determined by layout.
/// Selects scene content through a camera and render-layer mask.
/// </summary>
public partial class View : Element
{
    private readonly List<IObservable> _visibilityAncestors = [];

    /// <summary>Gets or sets the camera used to project selected scene content.</summary>
    [Observable]
    private ICamera? _camera;

    /// <summary>Gets or sets the scene render layers visible through this view.</summary>
    [Observable]
    private ulong _layerMask = RenderLayers.All;

    /// <summary>Gets or sets the rendering order relative to other views.</summary>
    [Observable]
    private int _renderOrder;

    /// <summary>Gets or sets whether drawable ordering is preserved within this view.</summary>
    [Observable]
    private bool _preserveDrawOrder;

    /// <summary>Gets or sets the blending mode used when rendering this view.</summary>
    [Observable]
    private Nexus.Graphics.BlendMode _blendMode = Nexus.Graphics.BlendMode.Alpha;

    /// <summary>Gets the renderer owned by this view.</summary>
    public ViewRenderer ViewComponent { get; }

    /// <summary>Initializes a view with its single owned renderer.</summary>
    public View()
        : base([new ViewRenderer()])
    {
        ViewComponent = Components.OfType<ViewRenderer>().Single();
        ViewComponent.HasExplicitClippingRegion = true;
        ViewComponent.Camera = Camera;
        ViewComponent.LayerMask = LayerMask;
        ViewComponent.RenderOrder = RenderOrder;
        ViewComponent.PreserveDrawOrder = PreserveDrawOrder;
        ViewComponent.BlendMode = BlendMode;
    }

    /// <summary>Synchronizes the assigned camera with the owned renderer.</summary>
    /// <param name="previousValue">The camera previously assigned to the view.</param>
    protected virtual partial void AfterCameraChanges(ICamera? previousValue) =>
        ViewComponent.Camera = Camera;

    /// <summary>Synchronizes the layer mask with the owned renderer.</summary>
    /// <param name="previousValue">The previous render-layer mask.</param>
    protected virtual partial void AfterLayerMaskChanges(ulong previousValue) =>
        ViewComponent.LayerMask = LayerMask;

    /// <summary>Synchronizes the rendering order with the owned renderer.</summary>
    /// <param name="previousValue">The previous view rendering order.</param>
    protected virtual partial void AfterRenderOrderChanges(int previousValue) =>
        ViewComponent.RenderOrder = RenderOrder;

    /// <summary>Synchronizes draw-order preservation with the owned renderer.</summary>
    /// <param name="previousValue">The previous draw-order preservation setting.</param>
    protected virtual partial void AfterPreserveDrawOrderChanges(bool previousValue) =>
        ViewComponent.PreserveDrawOrder = PreserveDrawOrder;

    /// <summary>Synchronizes the blending mode with the owned renderer.</summary>
    /// <param name="previousValue">The previous blending mode.</param>
    protected virtual partial void AfterBlendModeChanges(Nexus.Graphics.BlendMode previousValue) =>
        ViewComponent.BlendMode = BlendMode;

    /// <summary>Updates the clipping rectangle when the arranged element bounds change.</summary>
    /// <param name="previousValue">The bounds before the layout change.</param>
    protected override void AfterBoundsChanges(Rectangle<float> previousValue)
    {
        base.AfterBoundsChanges(previousValue);
        UpdateViewport();
    }

    /// <summary>Updates the clipping rectangle when this element's visibility changes.</summary>
    protected override void AfterIsVisibleChanges()
    {
        base.AfterIsVisibleChanges();
        UpdateViewport();
    }

    /// <inheritdoc />
    public override void Arrange(Rectangle<float> bounds)
    {
        base.Arrange(bounds);
        UpdateViewport();
    }

    /// <inheritdoc />
    public override void OnSceneHierarchyChanged()
    {
        base.OnSceneHierarchyChanged();
        UpdateVisibilityAncestorSubscriptions();
        UpdateViewport();
    }

    /// <summary>Subscribes to visibility changes throughout the current ancestor chain.</summary>
    private void UpdateVisibilityAncestorSubscriptions()
    {
        foreach (var ancestor in _visibilityAncestors)
            ancestor.PropertyChanged -= OnAncestorPropertyChanged;
        _visibilityAncestors.Clear();

        for (ISceneNode? ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor is not IObservable observable)
                continue;

            observable.PropertyChanged += OnAncestorPropertyChanged;
            _visibilityAncestors.Add(observable);
        }
    }

    /// <summary>Refreshes the viewport when an ancestor's visibility changes.</summary>
    /// <param name="propertyName">The name of the changed ancestor property.</param>
    private void OnAncestorPropertyChanged(string propertyName)
    {
        if (propertyName is "" or nameof(IsVisible))
            UpdateViewport();
    }

    /// <summary>Synchronizes the renderer's clipping rectangle with visible arranged bounds.</summary>
    private void UpdateViewport()
    {
        var bounds = IsEffectivelyVisible
            ? Bounds
            : new Rectangle<float>(Bounds.Origin, Vector2D<float>.Zero);
        ViewComponent.ClippingRegion = ToClippingRegion(bounds);
    }

    /// <summary>Converts screen-space bounds to an integer scissor rectangle without expanding them.</summary>
    /// <param name="bounds">The arranged screen-space bounds.</param>
    /// <returns>The integer rectangle constrained to the bounds' interior.</returns>
    private static Rectangle<int> ToClippingRegion(Rectangle<float> bounds)
    {
        if (
            !float.IsFinite(bounds.Origin.X)
            || !float.IsFinite(bounds.Origin.Y)
            || !float.IsFinite(bounds.Size.X)
            || !float.IsFinite(bounds.Size.Y)
            || bounds.Size.X <= 0f
            || bounds.Size.Y <= 0f
        )
            return EmptyBounds;

        var left = (int)Math.Clamp(Math.Ceiling((double)bounds.Origin.X), 0d, int.MaxValue);
        var top = (int)Math.Clamp(Math.Ceiling((double)bounds.Origin.Y), 0d, int.MaxValue);
        var right = (int)
            Math.Clamp(Math.Floor((double)bounds.Origin.X + bounds.Size.X), left, int.MaxValue);
        var bottom = (int)
            Math.Clamp(Math.Floor((double)bounds.Origin.Y + bounds.Size.Y), top, int.MaxValue);

        return new Rectangle<int>(left, top, right - left, bottom - top);
    }

    /// <summary>Gets the empty clipping rectangle used to suppress rendering.</summary>
    private static Rectangle<int> EmptyBounds => new(0, 0, 0, 0);
}
