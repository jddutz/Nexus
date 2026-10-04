namespace Nexus.Graphics.Components;

/// <summary>
/// Configures the rendering properties for a given view.
/// </summary>
public partial class ViewRenderer : Component
{
    [Observable]
    private string _name = nameof(ViewRenderer);

    [Observable(PublicSetter = true)]
    private ICamera? _camera = null;

    [Observable(PublicSetter = true)]
    private ulong _layerMask = 0;

    [Observable]
    private Rectangle<int> _clippingRegion;

    /// <summary>Gets or sets whether the clipping rectangle is explicit instead of full-target.</summary>
    [Observable]
    private bool _hasExplicitClippingRegion;

    [Observable]
    private uint _renderPassMask = RenderPasses.Main;

    [Observable]
    private int _renderOrder;

    /// <summary>Gets or sets whether selected drawables retain their draw order.</summary>
    [Observable]
    private bool _preserveDrawOrder;

    /// <summary>Gets or sets the blending mode used to render selected drawables.</summary>
    [Observable]
    private BlendMode _blendMode = BlendMode.Alpha;

    /// <summary>Gets or sets whether depth testing is enabled for selected drawables.</summary>
    [Observable]
    private bool _enableDepthTest = true;

    /// <summary>Gets or sets whether depth writes are enabled for selected drawables.</summary>
    [Observable]
    private bool _enableDepthWrite = true;

    /// <summary>Gets or sets the depth comparison operation for selected drawables.</summary>
    [Observable]
    private DepthComparison _depthComparison = DepthComparison.Less;

    /// <inheritdoc />
    public override string DisplayName => "View";
}
