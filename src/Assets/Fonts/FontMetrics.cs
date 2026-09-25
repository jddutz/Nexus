namespace Nexus.Assets.Fonts;

/// <summary>
/// Describes the font-wide metrics used for text layout.
/// </summary>
/// <param name="EmSize">The font's em size.</param>
/// <param name="Ascender">The distance from the baseline to the ascender.</param>
/// <param name="Descender">The distance from the baseline to the descender.</param>
/// <param name="LineHeight">The recommended distance between baselines.</param>
public readonly record struct FontMetrics(
    double EmSize,
    double Ascender,
    double Descender,
    double LineHeight
);
