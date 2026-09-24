using Nexus.AssetPipeline.Typography.DistanceFields;
using Nexus.Graphics.Text;

namespace Nexus.AssetPipeline.Typography.Atlas;

/// <summary>
/// Packs glyph bitmaps into a deterministic, row-based RGB8 atlas.
/// </summary>
public sealed class FontAtlasBuilder
{
    /// <summary>
    /// Packs glyphs in input order and returns their atlas bounds in the same order.
    /// </summary>
    /// <param name="glyphs">The glyph bitmaps to pack.</param>
    /// <returns>The packed atlas and one pixel-space bounds value per input bitmap.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="glyphs"/> is null.</exception>
    /// <exception cref="ArgumentException">The list is empty or contains a null bitmap.</exception>
    public FontAtlasBuildResult Build(IReadOnlyList<GlyphBitmap> glyphs)
    {
        ArgumentNullException.ThrowIfNull(glyphs);
        if (glyphs.Count == 0)
            throw new ArgumentException("At least one glyph bitmap is required.", nameof(glyphs));

        long totalArea = 0;
        var maximumWidth = 0;
        foreach (var glyph in glyphs)
        {
            ArgumentNullException.ThrowIfNull(glyph);
            maximumWidth = Math.Max(maximumWidth, glyph.Width);
            totalArea = checked(totalArea + (long)glyph.Width * glyph.Height);
        }

        var atlasWidth = Math.Max(
            1,
            Math.Max(maximumWidth, checked((int)Math.Ceiling(Math.Sqrt(totalArea))))
        );
        var positions = new (int X, int Y)[glyphs.Count];
        var x = 0;
        var y = 0;
        var rowHeight = 0;

        for (var index = 0; index < glyphs.Count; index++)
        {
            var glyph = glyphs[index];
            if (glyph.Width == 0 || glyph.Height == 0)
                continue;

            if (x > 0 && checked(x + glyph.Width) > atlasWidth)
            {
                y = checked(y + rowHeight);
                x = 0;
                rowHeight = 0;
            }

            positions[index] = (x, y);
            x = checked(x + glyph.Width);
            rowHeight = Math.Max(rowHeight, glyph.Height);
        }

        var atlasHeight = Math.Max(1, checked(y + rowHeight));
        var atlasPixels = new byte[checked(atlasWidth * atlasHeight * 3)];
        var bounds = new FontBounds[glyphs.Count];
        for (var index = 0; index < glyphs.Count; index++)
        {
            var glyph = glyphs[index];
            if (glyph.Width == 0 || glyph.Height == 0)
                continue;

            var (glyphX, glyphY) = positions[index];
            for (var row = 0; row < glyph.Height; row++)
            {
                var sourceOffset = checked(row * glyph.Width * 3);
                var destinationOffset = checked(((glyphY + row) * atlasWidth + glyphX) * 3);
                Buffer.BlockCopy(
                    glyph.Pixels,
                    sourceOffset,
                    atlasPixels,
                    destinationOffset,
                    checked(glyph.Width * 3)
                );
            }

            bounds[index] = new FontBounds(
                glyphX,
                atlasHeight - glyphY - glyph.Height,
                glyphX + glyph.Width,
                atlasHeight - glyphY
            );
        }

        return new FontAtlasBuildResult(
            new Fonts.FontAtlas(atlasWidth, atlasHeight, atlasPixels),
            bounds
        );
    }
}
