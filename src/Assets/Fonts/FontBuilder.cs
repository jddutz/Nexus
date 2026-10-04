using Nexus.Assets.Typography.Atlas;
using Nexus.Assets.Typography.DistanceFields;
using Nexus.Assets.Typography.FontReader.TrueType;
using Nexus.Assets.Typography.Geometry;
using Nexus.Core;

namespace Nexus.Assets.Fonts;

/// <summary>
/// Builds a font atlas and runtime metadata using the managed typography stages.
/// </summary>
public sealed class FontBuilder : IFontBuilder
{
    private readonly object _cacheLock = new();
    private readonly Dictionary<string, CachedFontSource> _fontSources = new(
        StringComparer.OrdinalIgnoreCase
    );

    /// <summary>
    /// Generates glyph distance fields, packs their atlas, and assembles the runtime font result.
    /// </summary>
    /// <param name="fontId">The content identifier of the source font.</param>
    /// <param name="sourcePath">The source TrueType or OpenType font path.</param>
    /// <param name="codepoints">The Unicode codepoints required by the font build.</param>
    /// <param name="settings">The atlas and distance-field generation settings.</param>
    /// <returns>The atlas, glyph metadata, font metrics, and codepoint kerning pairs.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="FontBuildException">The requested font result cannot be generated.</exception>
    public FontBuildResult Build(
        ContentId fontId,
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
        var fontPath = Path.GetFullPath(sourcePath);
        var source = GetFontSource(fontPath);
        var face = source.Reader.FontFace;
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
            var sourceGlyph = GetSourceGlyph(source, codepoint);
            var contours = sourceGlyph.Contours;
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
                    sourceGlyph.AdvanceWidth * (double)pixelsPerFontUnit,
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
                    : CreateAtlasBounds(glyph.PlaneBounds, bitmapBounds, atlasInset);
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
            fontId,
            atlasResult.Atlas,
            metrics,
            glyphs,
            GetKerningPairs(source, requestedCodepoints),
            new MsdfMetadata(settings.DistanceRange, settings.EmSize)
        );
        return result;
    }

    /// <summary>
    /// Gets cached source data, reloading and reparsing the file when its metadata changes.
    /// </summary>
    /// <param name="sourcePath">The normalized source font path.</param>
    /// <returns>The cached source font.</returns>
    private CachedFontSource GetFontSource(string sourcePath)
    {
        lock (_cacheLock)
        {
            var fileInfo = new FileInfo(sourcePath);
            if (!fileInfo.Exists)
                throw new FontBuildException($"Font source '{sourcePath}' does not exist.");

            var lastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
            if (
                _fontSources.TryGetValue(sourcePath, out var cached)
                && cached.Length == fileInfo.Length
                && cached.LastWriteTimeUtc == lastWriteTimeUtc
            )
                return cached;

            var data = File.ReadAllBytes(sourcePath);
            var source = new CachedFontSource(
                new TrueTypeFontReader(data),
                data.LongLength,
                lastWriteTimeUtc
            );
            _fontSources[sourcePath] = source;
            return source;
        }
    }

    /// <summary>Gets and caches one converted glyph outline from a source font.</summary>
    /// <param name="source">The cached source font.</param>
    /// <param name="codepoint">The Unicode codepoint to extract.</param>
    /// <returns>The source glyph data.</returns>
    private CachedSourceGlyph GetSourceGlyph(CachedFontSource source, int codepoint)
    {
        lock (_cacheLock)
        {
            if (source.Glyphs.TryGetValue(codepoint, out var cached))
                return cached;

            var glyphIndex = source.Reader.GetGlyphIndex(codepoint);
            var metrics = source.Reader.GetHorizontalMetrics(glyphIndex);
            var contours = source.Reader
                .GetGlyphOutline(glyphIndex)
                .Contours.Select(FontContourConverter.Convert)
                .ToArray();
            cached = new CachedSourceGlyph(metrics.AdvanceWidth, contours);
            source.Glyphs.Add(codepoint, cached);
            return cached;
        }
    }

    /// <summary>Gets kerning for the requested codepoints from a cached source font.</summary>
    /// <param name="source">The cached source font.</param>
    /// <param name="codepoints">The codepoints included in the build.</param>
    /// <returns>The kerning pairs for the requested codepoints.</returns>
    private IReadOnlyList<TextKerningPair> GetKerningPairs(
        CachedFontSource source,
        IReadOnlyList<int> codepoints
    )
    {
        lock (_cacheLock)
            return source.Reader.GetKerningPairs(codepoints);
    }

    /// <summary>Stores parsed source data and the file metadata used for invalidation.</summary>
    /// <param name="Reader">The parsed font reader.</param>
    /// <param name="Length">The source file length.</param>
    /// <param name="LastWriteTimeUtc">The source file's last-write timestamp.</param>
    private sealed class CachedFontSource(
        TrueTypeFontReader Reader,
        long Length,
        DateTime LastWriteTimeUtc
    )
    {
        /// <summary>Gets the parsed font reader.</summary>
        public TrueTypeFontReader Reader { get; } = Reader;

        /// <summary>Gets the source file length.</summary>
        public long Length { get; } = Length;

        /// <summary>Gets the source file's last-write timestamp.</summary>
        public DateTime LastWriteTimeUtc { get; } = LastWriteTimeUtc;

        /// <summary>Gets the converted glyph outlines cached by Unicode codepoint.</summary>
        public Dictionary<int, CachedSourceGlyph> Glyphs { get; } = [];
    }

    /// <summary>Stores source metrics and converted geometry for one glyph.</summary>
    /// <param name="AdvanceWidth">The glyph advance in source font units.</param>
    /// <param name="Contours">The converted glyph contours.</param>
    private sealed record CachedSourceGlyph(double AdvanceWidth, Contour[] Contours);

    /// <summary>
    /// Aligns the atlas crop with the plane origin and exact plane extent after raster rounding.
    /// </summary>
    /// <param name="planeBounds">The glyph's exact layout bounds.</param>
    /// <param name="bitmapBounds">The integer bounds assigned by the atlas packer.</param>
    /// <param name="atlasInset">The distance-field padding removed from each leading edge.</param>
    /// <returns>The atlas bounds matching the plane dimensions.</returns>
    private static FontBounds CreateAtlasBounds(
        FontBounds planeBounds,
        FontBounds bitmapBounds,
        double atlasInset
    )
    {
        var atlasLeft = bitmapBounds.Left + atlasInset;
        var atlasTop = bitmapBounds.Top - atlasInset;
        var planeWidth = planeBounds.Right - planeBounds.Left;
        var planeHeight = planeBounds.Top - planeBounds.Bottom;
        return new FontBounds(atlasLeft, atlasTop - planeHeight, atlasLeft + planeWidth, atlasTop);
    }
}
