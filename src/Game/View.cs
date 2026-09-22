namespace Nexus.Game;

/// <summary>
/// Provides a game object that owns the render configuration for a view.
/// </summary>
public class View : GameObject
{
    /// <summary>
    /// Gets the component that configures this view's render layers.
    /// </summary>
    public ViewComponent ViewComponent { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="View"/> class.
    /// </summary>
    public View()
    {
        ViewComponent = new ViewComponent();
        AddComponent(ViewComponent);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="View"/> class with the specified identifier.
    /// </summary>
    /// <param name="id">The identifier for the view.</param>
    public View(uint id)
        : base(id)
    {
        ViewComponent = new ViewComponent();
        AddComponent(ViewComponent);
    }
}
