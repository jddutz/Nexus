using Nexus.Graphics.Cameras;

namespace Nexus.Graphics.Components;

/// <summary>
/// Configures the rendering properties for a given view.
/// </summary>
public partial class ViewComponent : Component
{
    [Observable]
    private string _name = nameof(ViewComponent);

    [Observable(PublicSetter = true)]
    private ICameraComponent? _camera;

    [Observable(PublicSetter = true)]
    private ulong _layerMask;

    [Observable]
    private Rectangle<int> _clippingRegion;

    [Observable]
    private uint _renderPassMask = RenderPasses.Main;

    [Observable]
    private int _renderOrder;

    /// <summary>Initializes a view with an optional camera and layer mask.</summary>
    /// <param name="camera">The camera used to render the view.</param>
    /// <param name="layerMask">The drawable layer mask.</param>
    public ViewComponent(ICameraComponent? camera = null, ulong layerMask = 0)
    {
        _camera = camera;
        _layerMask = layerMask;
    }

    /// <inheritdoc />
    public override string DisplayName => "View";
}
