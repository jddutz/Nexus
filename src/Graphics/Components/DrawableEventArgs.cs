namespace Nexus.Graphics.Components;

/// <summary>Provides the drawable involved in a graphics-component membership change.</summary>
public sealed class DrawableEventArgs : EventArgs
{
    /// <summary>Initializes event data for a drawable membership change.</summary>
    /// <param name="drawable">The drawable that was added or removed.</param>
    public DrawableEventArgs(IDrawable drawable)
    {
        Drawable = drawable ?? throw new ArgumentNullException(nameof(drawable));
    }

    /// <summary>Gets the drawable that was added or removed.</summary>
    public IDrawable Drawable { get; }
}
