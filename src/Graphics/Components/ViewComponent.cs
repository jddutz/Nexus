namespace Nexus.Graphics.Components;

/// <summary>
/// Configures the render layers for a view.
/// </summary>
public class ViewComponent : Component
{
    /// <summary>
    /// Gets the render layers configured for the view.
    /// </summary>
    public RenderLayers RenderLayers { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewComponent"/> class.
    /// </summary>
    public ViewComponent()
    {
        RenderLayers.Add("Main", RenderPasses.Main);
    }
}
