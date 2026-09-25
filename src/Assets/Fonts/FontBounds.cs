namespace Nexus.Assets.Fonts;

/// <summary>
/// Represents an axis-aligned rectangle in font or atlas coordinates.
/// </summary>
/// <param name="Left">The left edge.</param>
/// <param name="Bottom">The bottom edge.</param>
/// <param name="Right">The right edge.</param>
/// <param name="Top">The top edge.</param>
public readonly record struct FontBounds(double Left, double Bottom, double Right, double Top);
