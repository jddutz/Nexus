using Nexus.Graphics.Cameras;

namespace Nexus.Graphics.Components;

/// <summary>
/// Configures the rendering properties for a given view.
/// </summary>
public class ViewComponent : Component
{
    private string _name = nameof(ViewComponent);
    private ICameraComponent? _camera;
    private ulong _layerMask;
    private Rectangle<int> _clippingRegion;
    private uint _renderPassMask = RenderPasses.Main;
    private int _renderOrder;

    /// <inheritdoc />
    public override string DisplayName => "View";

    /// <summary>
    /// Gets or sets the camera used to render this view.
    /// </summary>
    public ICameraComponent? Camera
    {
        get => _camera;
        set => SetProperty(ref _camera, value);
    }

    /// <summary>
    /// Gets or sets the mask of render layers included in this view.
    /// </summary>
    public ulong LayerMask
    {
        get => _layerMask;
        set => SetProperty(ref _layerMask, value);
    }

    /// <summary>
    /// Gets or sets the pixel-space clipping region for this view.
    /// </summary>
    public Rectangle<int> ClippingRegion
    {
        get => _clippingRegion;
        set => SetProperty(ref _clippingRegion, value);
    }

    /// <summary>
    /// Gets or sets a descriptive name for this view.
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>
    /// Gets or sets the render-pass mask enabled for this view.
    /// </summary>
    public uint RenderPassMask
    {
        get => _renderPassMask;
        set => SetProperty(ref _renderPassMask, value);
    }

    /// <summary>
    /// Gets or sets the order in which this view is rendered relative to other views.
    /// </summary>
    public int RenderOrder
    {
        get => _renderOrder;
        set => SetProperty(ref _renderOrder, value);
    }
}
