namespace Nexus.Graphics.Text;

/// <summary>
/// Represents an axis-aligned rectangle in text or atlas coordinates.
/// </summary>
public readonly record struct FontBounds(double Left, double Bottom, double Right, double Top);