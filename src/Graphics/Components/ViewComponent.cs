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
    private ICameraComponent? _camera = null;

    [Observable(PublicSetter = true)]
    private ulong _layerMask = 0;

    [Observable]
    private Rectangle<int> _clippingRegion;

    [Observable]
    private uint _renderPassMask = RenderPasses.Main;

    [Observable]
    private int _renderOrder;

    /// <inheritdoc />
    public override string DisplayName => "View";
}
