using Nexus.Assets.Typography.Atlas;
using Nexus.Assets.Typography.DistanceFields;
using Nexus.Assets.Typography.FontReader.OpenType;
using Nexus.Assets.Typography.Geometry;
using System.Numerics;
using Nexus.Core;
using Nexus.Core.Performance;

namespace Nexus.Assets.Fonts;

/// <summary>
/// Builds a font atlas and runtime metadata using the managed typography stages.
/// </summary>
public sealed class FontBuilder : IFontBuilder
{
    private readonly IPerformanceTelemetry? _telemetry;

    /// <summary>Creates a builder with optional, diagnostics-gated load telemetry.</summary>
    public FontBuilder(IPerformanceTelemetry? telemetry = null) => _telemetry = telemetry;

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
        using var buildTiming = new LoadPerformanceScope(_telemetry, "font.build.file", fontId.Value, settings.EmSize, units: codepoints.Count);
        if (codepoints.Count == 0)
            throw new FontBuildException("At least one glyph codepoint is required.");

        var requestedCodepoints = codepoints.Distinct().ToArray();
        var fontPath = Path.GetFullPath(sourcePath);
        var source = GetFontSource(fontPath);
        var face = source.Reader.FontFace;
        return BuildCore(fontId, requestedCodepoints, settings, face.UnitsPerEm,
            face.Ascender, face.Descender, face.LineGap,
            codepoint => GetSourceGlyph(source, codepoint, settings), GetKerningPairs(source, requestedCodepoints));
    }

    /// <summary>Rasterizes decoded outlines without accessing a font file. Settings override exported defaults.</summary>
    public FontBuildResult Build(
        ContentId fontId, FontRasterizerInput input, IReadOnlyList<int> codepoints,
        FontGenerationSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(codepoints);
        settings ??= new FontGenerationSettings
        {
            EmSize = input.GenerationSettings.EmSize,
            DistanceRange = input.GenerationSettings.DistanceRange,
            Padding = input.GenerationSettings.Padding,
        };
        settings.Validate();
        using var buildTiming = new LoadPerformanceScope(_telemetry, "font.build.embedded", fontId.Value, settings.EmSize, units: codepoints.Count);
        if (codepoints.Count == 0) throw new FontBuildException("At least one glyph codepoint is required.");
        if (input.Metrics.UnitsPerEm == 0) throw new FontBuildException("Font units per em must be positive.");
        var requested = codepoints.Distinct().ToArray();
        var glyphs = input.Glyphs.ToDictionary(glyph => glyph.Codepoint);
        CachedSourceGlyph Resolve(int codepoint)
        {
            if (!glyphs.TryGetValue(codepoint, out var glyph))
                throw new FontBuildException($"Decoded font '{fontId}' has no codepoint U+{codepoint:X}.");
            var contours = glyph.Contours.Select(contour =>
                new Contour(contour.Segments.Select(segment => segment.Kind switch
                {
                    "line" when segment.Control is null => (Edge)new LineSegment(Point(segment.Start), Point(segment.End)),
                    "quadratic" when segment.Control is { } control => new QuadraticSegment(Point(segment.Start), Point(control), Point(segment.End)),
                    _ => throw new FontBuildException($"Invalid decoded font segment kind '{segment.Kind}'."),
                }))).ToArray();
            var tolerance = settings.GetOutlineTolerance(input.Metrics.UnitsPerEm);
            using var simplifyTiming = new LoadPerformanceScope(_telemetry, "font.outline.simplify", fontId.Value,
                settings.EmSize, codepoint);
            return new CachedSourceGlyph(glyph.AdvanceWidth,
                contours.Select(contour => QuadraticContourSimplifier.Simplify(contour, tolerance)).ToArray());
        }
        var selected = requested.ToHashSet();
        return BuildCore(fontId, requested, settings, input.Metrics.UnitsPerEm,
            input.Metrics.Ascender, input.Metrics.Descender, input.Metrics.LineGap, Resolve,
            input.Kerning.Where(pair => selected.Contains(pair.LeftCodepoint) && selected.Contains(pair.RightCodepoint)).ToArray());
    }

    private static Vector2 Point(FontRasterizerPoint point) => new(point.X, point.Y);

    private FontBuildResult BuildCore(ContentId fontId, int[] requestedCodepoints,
        FontGenerationSettings settings, ushort unitsPerEm, short ascender, short descender, short lineGap,
        Func<int, CachedSourceGlyph> resolveGlyph, IReadOnlyList<TextKerningPair> kerning)
    {
        var pixelsPerFontUnit = settings.EmSize / (float)unitsPerEm;
        var distanceRange = (float)settings.DistanceRange;
        var bitmapPadding = checked(settings.Padding + (int)Math.Ceiling(settings.DistanceRange));
        var msdfSettings = new MsdfGenerationSettings
        {
            PixelsPerUnit = pixelsPerFontUnit,
            DistanceRange = distanceRange,
            Padding = bitmapPadding,
        };
        var msdfGenerator = new MsdfGenerator();
        var glyphData = new (
            int Codepoint, double Advance, FontBounds PlaneBounds, GlyphBitmap Bitmap
        )[requestedCodepoints.Length];
        var enabled = _telemetry?.IsEnabled == true;
        var allocations = enabled ? new long[requestedCodepoints.Length] : null;
        var batchTiming = new LoadPerformanceScope(_telemetry, "font.glyphs.build", fontId.Value,
            settings.EmSize, units: requestedCodepoints.Length);
        try
        {
            void Rasterize(int index)
            {
                var before = enabled ? GC.GetAllocatedBytesForCurrentThread() : 0;
                try
                {
                    var codepoint = requestedCodepoints[index];
                    // Embedded reconstruction and simplification are independent. The file
                    // resolver protects only its shared reader and decoded-outline cache.
                    CachedSourceGlyph sourceGlyph;
                    using (var outlineTiming = new LoadPerformanceScope(_telemetry, "font.outline.glyph",
                        fontId.Value, settings.EmSize, codepoint))
                        sourceGlyph = resolveGlyph(codepoint);
                    GeometryBounds? geometryBounds;
                    using (var boundsTiming = new LoadPerformanceScope(_telemetry, "font.bounds.glyph",
                        fontId.Value, settings.EmSize, codepoint))
                        geometryBounds = GeometryBoundsCalculator.GetBounds(sourceGlyph.Contours);
                    GlyphBitmap bitmap;
                    using (var glyphTiming = new LoadPerformanceScope(_telemetry, "font.msdf.glyph",
                        fontId.Value, settings.EmSize, codepoint,
                        enabled ? sourceGlyph.Contours.Sum(contour => (long)contour.Edges.Count) : 0))
                        bitmap = msdfGenerator.Generate(sourceGlyph.Contours, msdfSettings);
                    var expansion = settings.DistanceRange;
                    var planeBounds = geometryBounds is { } bounds
                        ? new FontBounds(
                            bounds.Left * pixelsPerFontUnit - expansion,
                            bounds.Bottom * pixelsPerFontUnit - expansion,
                            bounds.Right * pixelsPerFontUnit + expansion,
                            bounds.Top * pixelsPerFontUnit + expansion)
                        : default;
                    // Each worker owns one slot. Packing consumes the original codepoint order.
                    glyphData[index] = (codepoint, sourceGlyph.AdvanceWidth * (double)pixelsPerFontUnit,
                        planeBounds, bitmap);
                }
                finally
                {
                    if (allocations is not null)
                        allocations[index] = GC.GetAllocatedBytesForCurrentThread() - before;
                }
            }

            var workers = settings.MaxDegreeOfParallelism == 0
                ? Math.Clamp(Environment.ProcessorCount - 1, 1, 8)
                : settings.MaxDegreeOfParallelism;
            if (workers == 1 || requestedCodepoints.Length < 8)
                for (var index = 0; index < requestedCodepoints.Length; index++) Rasterize(index);
            else
            {
                try
                {
                    Parallel.For(0, requestedCodepoints.Length,
                        new ParallelOptions { MaxDegreeOfParallelism = workers }, Rasterize);
                }
                catch (AggregateException exception)
                {
                    throw new FontBuildException("Parallel glyph generation failed.", exception);
                }
            }
        }
        finally
        {
            batchTiming.Dispose(allocations?.Sum() ?? 0);
        }
        FontAtlasBuildResult atlasResult;
        using (var atlasTiming = new LoadPerformanceScope(_telemetry, "font.atlas.pack", fontId.Value, settings.EmSize, units: glyphData.Length))
            atlasResult = new FontAtlasBuilder().Build(glyphData.Select(glyph => glyph.Bitmap).ToArray());
        var atlasInset = bitmapPadding - settings.DistanceRange;
        var glyphs = new FontGlyph[glyphData.Length];
        for (var index = 0; index < glyphData.Length; index++)
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

        var emScale = settings.EmSize / (double)unitsPerEm;
        var metrics = new FontMetrics(
            settings.EmSize,
            ascender * emScale,
            descender * emScale,
            (ascender - descender + lineGap) * emScale
        );
        var result = new FontBuildResult(
            fontId,
            atlasResult.Atlas,
            metrics,
            glyphs,
            kerning,
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
        using var sourceTiming = new LoadPerformanceScope(_telemetry, "font.source.load", sourcePath);
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
            {
                _telemetry?.RecordCache("font.source", sourcePath, true);
                return cached;
            }

            _telemetry?.RecordCache("font.source", sourcePath, false);
            byte[] data;
            using (var readTiming = new LoadPerformanceScope(_telemetry, "font.file.read", sourcePath, units: fileInfo.Length))
                data = File.ReadAllBytes(sourcePath);
            using var parseTiming = new LoadPerformanceScope(_telemetry, "font.source.parse", sourcePath, units: data.Length);
            var source = new CachedFontSource(
                new OpenTypeFontReader(data),
                data.LongLength,
                lastWriteTimeUtc
            );
            _fontSources[sourcePath] = source;
            return source;
        }
    }

    /// <summary>Gets and caches one converted glyph outline at the requested raster resolution.</summary>
    /// <param name="source">The cached source font.</param>
    /// <param name="codepoint">The Unicode codepoint to extract.</param>
    /// <param name="settings">Raster size used to bound the approximation error.</param>
    /// <returns>The source glyph data.</returns>
    private CachedSourceGlyph GetSourceGlyph(CachedFontSource source, int codepoint, FontGenerationSettings settings)
    {
        lock (source)
        {
            var key = (codepoint, settings.EmSize);
            if (source.Glyphs.TryGetValue(key, out var cached))
            {
                _telemetry?.RecordCache("font.outline", null, true);
                return cached;
            }
            _telemetry?.RecordCache("font.outline", null, false);

            var glyphIndex = source.Reader.GetGlyphIndex(codepoint);
            var metrics = source.Reader.GetHorizontalMetrics(glyphIndex);
            var contours = source.Reader
                .GetGlyphContours(glyphIndex, settings.GetOutlineTolerance(source.Reader.FontFace.UnitsPerEm))
                .ToArray();
            cached = new CachedSourceGlyph(metrics.AdvanceWidth, contours);
            source.Glyphs.Add(key, cached);
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
        lock (source)
        {
            using var kerningTiming = new LoadPerformanceScope(_telemetry, "font.kerning", units: codepoints.Count);
            return source.Reader.GetKerningPairs(codepoints);
        }
    }

    /// <summary>Stores parsed source data and the file metadata used for invalidation.</summary>
    /// <param name="Reader">The parsed font reader.</param>
    /// <param name="Length">The source file length.</param>
    /// <param name="LastWriteTimeUtc">The source file's last-write timestamp.</param>
    private sealed class CachedFontSource(
        OpenTypeFontReader Reader,
        long Length,
        DateTime LastWriteTimeUtc
    )
    {
        /// <summary>Gets the parsed font reader.</summary>
        public OpenTypeFontReader Reader { get; } = Reader;

        /// <summary>Gets the source file length.</summary>
        public long Length { get; } = Length;

        /// <summary>Gets the source file's last-write timestamp.</summary>
        public DateTime LastWriteTimeUtc { get; } = LastWriteTimeUtc;

        /// <summary>Gets the converted glyph outlines cached by Unicode codepoint.</summary>
        public Dictionary<(int Codepoint, int EmSize), CachedSourceGlyph> Glyphs { get; } = [];
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
