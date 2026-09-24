namespace Nexus.Graphics.Text;

/// <summary>
/// Describes the font-wide metrics used for text layout.
/// </summary>
public readonly record struct FontMetrics(
    double EmSize,
    double Ascender,
    double Descender,
    double LineHeight
);