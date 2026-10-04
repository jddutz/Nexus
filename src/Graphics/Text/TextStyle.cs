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
        Id = ComputeId(font, texture, size);
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

    /// <summary>Computes a stable identifier from the font output and style settings.</summary>
    /// <param name="font">The generated font data.</param>
    /// <param name="texture">The texture used for the glyph atlas.</param>
    /// <param name="size">The requested text size.</param>
    /// <returns>The identifier of the resulting text style.</returns>
    private static TextStyleId ComputeId(
        FontBuildResult font,
        ITexture texture,
        double size
    )
    {
        var hash = new IdentityHashBuilder(nameof(TextStyle));
        hash.Add(font.Atlas.Width);
        hash.Add(font.Atlas.Height);
        hash.Add(font.Atlas.Pixels.Length);
        hash.Add(font.Atlas.Pixels);
        hash.Add(font.Metrics.EmSize);
        hash.Add(font.Metrics.Ascender);
        hash.Add(font.Metrics.Descender);
        hash.Add(font.Metrics.LineHeight);
        hash.Add(font.Glyphs.Count);
        foreach (var glyph in font.Glyphs)
        {
            hash.Add(glyph.Codepoint);
            hash.Add(glyph.Advance);
            hash.Add(glyph.PlaneBounds.Left);
            hash.Add(glyph.PlaneBounds.Bottom);
            hash.Add(glyph.PlaneBounds.Right);
            hash.Add(glyph.PlaneBounds.Top);
            hash.Add(glyph.AtlasBounds.Left);
            hash.Add(glyph.AtlasBounds.Bottom);
            hash.Add(glyph.AtlasBounds.Right);
            hash.Add(glyph.AtlasBounds.Top);
        }

        hash.Add(font.Kerning.Count);
        foreach (var pair in font.Kerning)
        {
            hash.Add(pair.LeftCodepoint);
            hash.Add(pair.RightCodepoint);
            hash.Add(pair.AdvanceAdjustment);
        }

        hash.Add(font.Msdf.DistanceRange);
        hash.Add(font.Msdf.GenerationEmSize);
        hash.Add(texture.Id.Value);
        hash.Add(texture.Width);
        hash.Add(texture.Height);
        hash.Add((uint)texture.TextureFormat);
        hash.Add(size);
        return new TextStyleId(hash.Compute());
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
