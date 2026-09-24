using Nexus.AssetPipeline.Typography.Atlas;
using Nexus.AssetPipeline.Typography.DistanceFields;
using Nexus.AssetPipeline.Typography.FontReader.TrueType;
using Nexus.AssetPipeline.Typography.Geometry;

namespace Nexus.AssetPipeline.Fonts;

/// <summary>
/// Builds a font atlas and runtime metadata using the managed typography stages.
/// </summary>
public sealed class FontBuilder : IFontBuilder
{
    /// <summary>
    /// Generates glyph distance fields, packs their atlas, and assembles the runtime font result.
    /// </summary>
    /// <param name="sourcePath">The source TrueType or OpenType font path.</param>
    /// <param name="codepoints">The Unicode codepoints required by the font build.</param>
    /// <param name="settings">The atlas and distance-field generation settings.</param>
    /// <returns>The atlas, glyph metadata, font metrics, and codepoint kerning pairs.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="FontBuildException">The requested font result cannot be generated.</exception>
    public FontBuildResult Build(
        string sourcePath,
        IReadOnlyList<int> codepoints,
        FontGenerationSettings settings
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(codepoints);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        if (codepoints.Count == 0)
            throw new FontBuildException("At least one glyph codepoint is required.");

        var requestedCodepoints = codepoints.Distinct().ToArray();
        var reader = TrueTypeFontReader.Open(sourcePath);
        var face = reader.FontFace;
        var pixelsPerFontUnit = settings.EmSize / (float)face.UnitsPerEm;
        var distanceRange = (float)settings.DistanceRange;
        var bitmapPadding = checked(settings.Padding + (int)Math.Ceiling(settings.DistanceRange));
        var msdfSettings = new MsdfGenerationSettings
        {
            PixelsPerUnit = pixelsPerFontUnit,
            DistanceRange = distanceRange,
            Padding = bitmapPadding,
        };
        var msdfGenerator = new MsdfGenerator();
        var glyphData = new List<(
            int Codepoint,
            double Advance,
            FontBounds PlaneBounds,
            GlyphBitmap Bitmap
        )>(requestedCodepoints.Length);

        foreach (var codepoint in requestedCodepoints)
        {
            var glyphIndex = reader.GetGlyphIndex(codepoint);
            var horizontalMetrics = reader.GetHorizontalMetrics(glyphIndex);
            var contours = reader
                .GetGlyphOutline(glyphIndex)
                .Contours.Select(FontContourConverter.Convert)
                .ToArray();
            var geometryBounds = GeometryBoundsCalculator.GetBounds(contours);
            var bitmap = msdfGenerator.Generate(contours, msdfSettings);
            var expansion = settings.DistanceRange;
            var planeBounds = geometryBounds is { } bounds
                ? new FontBounds(
                    bounds.Left * pixelsPerFontUnit - expansion,
                    bounds.Bottom * pixelsPerFontUnit - expansion,
                    bounds.Right * pixelsPerFontUnit + expansion,
                    bounds.Top * pixelsPerFontUnit + expansion
                )
                : default;

            glyphData.Add(
                (
                    codepoint,
                    horizontalMetrics.AdvanceWidth * (double)pixelsPerFontUnit,
                    planeBounds,
                    bitmap
                )
            );
        }

        var atlasResult = new FontAtlasBuilder().Build(
            glyphData.Select(glyph => glyph.Bitmap).ToArray()
        );
        var atlasInset = bitmapPadding - settings.DistanceRange;
        var glyphs = new FontGlyph[glyphData.Count];
        for (var index = 0; index < glyphData.Count; index++)
        {
            var glyph = glyphData[index];
            var bitmapBounds = atlasResult.AtlasBounds[index];
            var atlasBounds =
                glyph.Bitmap.Width == 0 || glyph.Bitmap.Height == 0
                    ? default
                    : new FontBounds(
                        bitmapBounds.Left + atlasInset,
                        bitmapBounds.Bottom + atlasInset,
                        bitmapBounds.Right - atlasInset,
                        bitmapBounds.Top - atlasInset
                    );
            glyphs[index] = new FontGlyph(
                glyph.Codepoint,
                glyph.Advance,
                glyph.PlaneBounds,
                atlasBounds
            );
        }

        var emScale = settings.EmSize / (double)face.UnitsPerEm;
        var metrics = new FontMetrics(
            settings.EmSize,
            face.Ascender * emScale,
            face.Descender * emScale,
            (face.Ascender - face.Descender + face.LineGap) * emScale
        );
        var result = new FontBuildResult(
            atlasResult.Atlas,
            metrics,
            glyphs,
            reader.GetKerningPairs(requestedCodepoints),
            new MsdfMetadata(settings.DistanceRange, settings.EmSize)
        );
        PipelineLog.Info(
            $"FontBuilder.Build returned atlas {result.Atlas.Width}x{result.Atlas.Height}, "
                + $"Glyphs={result.Glyphs.Count}, Kerning={result.Kerning.Count}."
        );
        return result;
    }
}
