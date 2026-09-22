namespace Nexus.Graphics;

/// <summary>
/// Settings that configure the application window.
/// </summary>
public sealed record WindowSettings
{
    /// <summary>Gets or sets the window title.</summary>
    public string Title { get; set; } = "Nexus Game";

    /// <summary>Gets or sets the window width in pixels.</summary>
    public int Width { get; set; } = 1280;

    /// <summary>Gets or sets the window height in pixels.</summary>
    public int Height { get; set; } = 720;

    /// <summary>Gets or sets a value indicating whether vertical synchronization is enabled.</summary>
    public bool VSync { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the window can be resized.</summary>
    public bool Resizable { get; set; } = true;

    /// <summary>Gets or sets the window display mode.</summary>
    public WindowState Mode { get; set; } = WindowState.Normal;
}