namespace Nexus.Graphics.Components;

/// <summary>
/// Configures the rendering properties for a given view.
/// </summary>
public class ViewComponent : Component
{
    private string _name = nameof(ViewComponent);
    private uint _renderPassMask = RenderPasses.Main;

    /// <summary>
    /// Gets or sets the name of the render layer created for this view.
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>
    /// Gets or sets the render-pass mask used by the render layer created for this view.
    /// </summary>
    public uint RenderPassMask
    {
        get => _renderPassMask;
        set => SetProperty(ref _renderPassMask, value);
    }

    /// <summary>
    /// Gets or sets the render layer assigned to this view.
    /// </summary>
    public IRenderLayer? RenderLayer { get; set; }
}
