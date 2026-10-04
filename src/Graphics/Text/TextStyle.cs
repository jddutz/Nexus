using System.Collections.ObjectModel;

namespace Nexus.Graphics.Text;

using Nexus.Assets.Fonts;

/// <summary>
/// Provides generated font data and visual settings for rendering text.
/// </summary>
public sealed class TextStyle : ITextStyle
{
    /// <inheritdoc/>
    public TextStyleId Id { get; }

    /// <summary>
    /// Initializes a text style from generated font data and its atlas texture.
    /// </summary>
    /// <param name="font">The generated glyph and font metrics.</param>
    /// <param name="texture">The texture containing the generated glyph atlas.</param>
    /// <param name="size">The requested text size.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The text size is not positive and finite.</exception>
    public TextStyle(FontBuildResult font, ITexture texture, double size)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(texture);
        if (!double.IsFinite(size) || size <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(size),
                "Text size must be positive and finite."
            );

        Texture = texture;
        Id = new IdentityHashBuilder(nameof(TextStyle)).Add(font.FontId).Add(size).Compute();
        Glyphs = new ReadOnlyDictionary<int, FontGlyph>(
            font.Glyphs.ToDictionary(glyph => glyph.Codepoint)
        );
        FontMetrics = font.Metrics;
        Msdf = font.Msdf;
        Kerning = new ReadOnlyDictionary<(int LeftCodepoint, int RightCodepoint), double>(
            font.Kerning.ToDictionary(
                pair => (pair.LeftCodepoint, pair.RightCodepoint),
                pair => pair.AdvanceAdjustment
            )
        );
        Size = size;
    }

    /// <inheritdoc/>
    public ITexture Texture { get; }

    /// <inheritdoc/>
    public IReadOnlyDictionary<int, FontGlyph> Glyphs { get; }

    /// <inheritdoc/>
    public FontMetrics FontMetrics { get; }

    /// <inheritdoc/>
    public MsdfMetadata Msdf { get; }

    /// <inheritdoc/>
    public IReadOnlyDictionary<(int LeftCodepoint, int RightCodepoint), double> Kerning { get; }

    /// <inheritdoc/>
    public double Size { get; }
}
