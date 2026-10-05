using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Nexus.Assets.Fonts;
using Nexus.Assets.Typography.FontReader.OpenType;
using Nexus.Assets.Typography.Geometry;

namespace Nexus.AssetPipeline.Tests;

/// <summary>Exercises CFF OpenType decoding using deterministic in-memory fonts.</summary>
public sealed class OpenTypeFontReaderTests
{
    [Theory]
    [InlineData(".otf", false)]
    [InlineData(".ttf", false)]
    [InlineData(".otf", true)]
    [InlineData(".ttf", true)]
    public void PipelineDetectsCffContentRegardlessOfExtension(string extension, bool includeMsdf)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"nexus-otf-pipeline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            var source = "font" + extension;
            var program = Join(N(0, 0), [21], N(0, 100, 100, 0, 0, -100), [8, 14]);
            File.WriteAllBytes(Path.Combine(folder, source), Font(Cff(program)));
            var definition = Path.Combine(folder, "nap.yaml");
            File.WriteAllText(definition, $"""
                root: .
                assets:
                  - assetType: font
                    contentId: test.cff
                    source: {source}
                    includeRasterizerInput: true
                    includeMsdf: {includeMsdf.ToString().ToLowerInvariant()}
                """);
            var output = Path.Combine(folder, "content");
            Assert.Equal(0, new Pipeline([definition], output).Execute());
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "content-manifest.json")));
            var entry = manifest.RootElement.GetProperty("Fonts").GetProperty("Content").GetProperty("test.cff");
            var input = JsonSerializer.Deserialize<FontRasterizerInput>(entry.GetProperty("RasterizerInput").GetRawText());
            Assert.NotNull(input);
            var glyph = Assert.Single(input.Glyphs, value => value.Codepoint == 'A');
            Assert.Equal(1, glyph.GlyphIndex);
            Assert.Equal(600, glyph.AdvanceWidth);
            Assert.NotNull(glyph.Bounds);
            Assert.InRange(glyph.Bounds.Top, 74.99f, 75.01f);
            Assert.Contains(glyph.Contours.SelectMany(contour => contour.Segments), segment => segment.Kind == "quadratic" && segment.Control is not null);
            Assert.Equal(includeMsdf, File.Exists(Path.Combine(output, "fonts", "test.cff.png")));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void ReadsMetricsMappingEmptyGlyphAndClosedOutline()
    {
        var reader = new OpenTypeFontReader(Font(Cff(Rectangle())));
        Assert.Equal(1000, reader.FontFace.UnitsPerEm);
        Assert.Equal(2, reader.FontFace.GlyphCount);
        Assert.Equal(1, reader.GetGlyphIndex('A'));
        Assert.Equal(0, reader.GetGlyphIndex(' '));
        Assert.Equal(600, reader.GetHorizontalMetrics(1).AdvanceWidth);
        Assert.Empty(reader.GetGlyphContours(0));
        Assert.Empty(reader.GetKerningPairs(['A']));
        var contour = Assert.Single(reader.GetGlyphContours(1));
        Assert.Equal(4, contour.Edges.Count);
        Assert.Equal(new GeometryBounds(100, 200, 500, 700), GeometryBoundsCalculator.GetBounds([contour]));
        Assert.True(GeometryDistance.SignedDistanceToContour(new Vector2(300, 400), contour) < 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetGlyphContours(2));
    }

    [Fact]
    public void ConvertsCubicArchToAccurateQuadratics()
    {
        var program = Join(N(0, 0), [21], N(0, 100, 100, 0, 0, -100), [8, 14]);
        var contour = Assert.Single(new OpenTypeFontReader(Font(Cff(program))).GetGlyphContours(1));
        Assert.Contains(contour.Edges, edge => edge is QuadraticSegment);
        var bounds = GeometryBoundsCalculator.GetBounds([contour])!.Value;
        Assert.InRange(bounds.Top, 74.99f, 75.01f);
        Assert.Equal(100, bounds.Right);
        Assert.InRange(MathF.Abs(GeometryDistance.SignedDistanceToContour(new Vector2(50, 75), contour)), 0, .01f);
    }

    [Theory]
    [InlineData(.01f)]
    [InlineData(.1f)]
    [InlineData(.25f)]
    public void CubicToleranceBoundsApproximationError(float tolerance)
    {
        var program = Join(N(0, 0), [21], N(0, 100, 100, 0, 0, -100), [8, 14]);
        var reader = new OpenTypeFontReader(Font(Cff(program)));
        var precise = Assert.Single(reader.GetGlyphContours(1));
        var contour = Assert.Single(reader.GetGlyphContours(1, tolerance));
        Assert.True(contour.Edges.Count <= precise.Edges.Count);
        if (tolerance > .01f) Assert.True(contour.Edges.Count < precise.Edges.Count);
        Assert.Equal(contour.Edges[0].Start, contour.Edges[^1].End);
        var curves = contour.Edges.OfType<QuadraticSegment>().ToArray();
        for (var index = 0; index <= 1000; index++)
        {
            var t = index / 1000f;
            var inverse = 1f - t;
            var point = new Vector2(300f * inverse * t * t + 100f * t * t * t,
                300f * inverse * inverse * t + 300f * inverse * t * t);
            var distance = curves.Min(edge => MathF.Abs(GeometryDistance.SignedDistanceToEdge(point, edge)));
            Assert.InRange(distance, 0f, tolerance + .0001f);
        }
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void RejectsInvalidCubicTolerance(float tolerance)
    {
        var reader = new OpenTypeFontReader(Font(Cff(Rectangle())));
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetGlyphContours(1, tolerance));
    }

    [Fact]
    public void PreservesFractionalFixedPointCoordinates()
    {
        var program = Join([255, 0, 0, 128, 0], N(0), [21], N(20, 0, 0, 20), [5, 14]);
        var contour = Assert.Single(new OpenTypeFontReader(Font(Cff(program))).GetGlyphContours(1));
        Assert.Equal(.5f, contour.Edges[0].Start.X);
    }

    [Fact]
    public void SharesStackAndHintsAcrossLocalAndGlobalSubroutines()
    {
        var local = Join(N(0, 50), [3, 19, 128], N(100, 200), [21], N(-107), [29, 11]);
        var global = Join(N(400, 0, 0, 500, -400, 0), [5, 11]);
        var program = Join(N(600, 0, 50), [1], N(-107), [10, 14]);
        var contour = Assert.Single(new OpenTypeFontReader(Font(Cff(program, [local], [global]))).GetGlyphContours(1));
        Assert.Equal(new GeometryBounds(100, 200, 500, 700), GeometryBoundsCalculator.GetBounds([contour]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectsCidPrivateSubroutines(bool ranges)
    {
        var program = Join(N(-107), [10, 14]);
        var subroutine = Join(Rectangle()[..^1], [11]);
        var contour = Assert.Single(new OpenTypeFontReader(Font(CidCff(program, subroutine, ranges))).GetGlyphContours(1));
        Assert.Equal(new GeometryBounds(100, 200, 500, 700), GeometryBoundsCalculator.GetBounds([contour]));
    }

    public static IEnumerable<object[]> PathPrograms()
    {
        yield return [Join(N(10, 20, 30), [6])];
        yield return [Join(N(10, 20, 30), [7])];
        yield return [Join(N(0, 20, 20, 0, 0, -20, 20, 0), [24])];
        yield return [Join(N(20, 0, 0, 20, 20, 0, 0, -20), [25])];
        yield return [Join(N(10, 20, 20, 30), [26])];
        yield return [Join(N(5, 10, 20, 20, 30), [26])];
        yield return [Join(N(10, 20, 20, 30), [27])];
        yield return [Join(N(5, 10, 20, 20, 30), [27])];
        yield return [Join(N(10, 20, 20, 30, 10, 20, 20, 30, 5), [30])];
        yield return [Join(N(10, 20, 20, 30, 10, 20, 20, 30, 5), [31])];
        yield return [Join(N(10, 20, 5, 30, 20, 10, 30), [12, 34])];
        yield return [Join(N(10, 5, 20, 5, 30, -10, 10, -5, 20, -5, 30, 10, 50), [12, 35])];
        yield return [Join(N(10, 5, 20, 5, 30, 10, 20, -10, 30), [12, 36])];
        yield return [Join(N(10, 5, 20, 5, 30, -10, 10, -5, 20, -5, 30), [12, 37])];
    }

    [Theory]
    [MemberData(nameof(PathPrograms))]
    public void DecodesCompactPathAndFlexOperators(byte[] path)
    {
        var contour = Assert.Single(new OpenTypeFontReader(Font(Cff(Join(N(0, 0), [21], path, [14])))).GetGlyphContours(1));
        Assert.NotEmpty(contour.Edges);
        Assert.Equal(contour.Edges[0].Start, contour.Edges[^1].End);
        Assert.All(contour.Edges, edge => Assert.True(float.IsFinite(edge.End.X) && float.IsFinite(edge.End.Y)));
    }

    [Fact]
    public void ArithmeticAndTransientStorageFeedPathOperands()
    {
        // Store 50, then retrieve it and add 50 for the initial x coordinate.
        var program = Join(N(50, 0), [12, 20], N(0), [12, 21], N(50), [12, 10], N(200), [21], N(400, 0, 0, 500, -400, 0), [5, 14]);
        var contour = Assert.Single(new OpenTypeFontReader(Font(Cff(program))).GetGlyphContours(1));
        Assert.Equal(new Vector2(100, 200), contour.Edges[0].Start);
    }

    public static IEnumerable<object[]> InvalidPrograms()
    {
        yield return [new byte[] { 28 }];
        yield return [new byte[] { 11 }];
        yield return [Join(N(0, 0), [21], N(1), [5, 14])];
        yield return [Join(N(-108), [10, 14])];
        yield return [Join(N(0, 0), [21], N(1, 2, 3, 4), [14])];
        yield return [Join(N(0, 10), [1, 19])];
        yield return [Join(N(0, 0), [21], N(0), [12, 12, 14])];
        yield return [Join(Enumerable.Repeat((byte)139, 49).ToArray(), [14])];
    }

    [Theory]
    [MemberData(nameof(InvalidPrograms))]
    public void RejectsMalformedCharstrings(byte[] program)
    {
        Assert.Throws<InvalidDataException>(() => new OpenTypeFontReader(Font(Cff(program))).GetGlyphContours(1));
    }

    [Fact]
    public void RejectsRecursiveSubroutines()
    {
        var recursion = Join(N(-107), [10, 11]);
        Assert.Throws<InvalidDataException>(() => new OpenTypeFontReader(Font(Cff(Join(N(-107), [10, 14]), [recursion]))).GetGlyphContours(1));
    }

    [Fact]
    public void RejectsTruncatedIndexesAndGlyphCountMismatch()
    {
        var valid = Cff(Rectangle());
        for (var length = 0; length < valid.Length; length++)
            Assert.Throws<InvalidDataException>(() => new OpenTypeFontReader(Font(valid[..length])).GetGlyphContours(1));
        Assert.Throws<InvalidDataException>(() => new OpenTypeFontReader(Font(valid, glyphCount: 3)).GetGlyphContours(1));
    }

    [Fact]
    public void ReportsUnsupportedCff2Explicitly()
    {
        var error = Assert.Throws<InvalidDataException>(() => new OpenTypeFontReader(Font([], "CFF2")).GetGlyphContours(1));
        Assert.Contains("CFF2", error.Message);
    }

    [Fact]
    public void BuildsOtfAtlasWithEmptySpaceGlyph()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nexus-{Guid.NewGuid():N}.otf");
        File.WriteAllBytes(path, Font(Cff(Rectangle())));
        try
        {
            var result = new FontBuilder().Build("otf", path, ['A', ' '], new FontGenerationSettings { EmSize = 100, DistanceRange = 4, Padding = 2 });
            Assert.Equal(80, result.Metrics.Ascender);
            Assert.Equal(-20, result.Metrics.Descender);
            Assert.Equal(60, result.Glyphs[0].Advance, 5);
            Assert.Equal(new FontBounds(6, 16, 54, 74), result.Glyphs[0].PlaneBounds);
            Assert.Equal(default, result.Glyphs[1].AtlasBounds);
            Assert.Contains(result.Atlas.Pixels, pixel => pixel != 0);
        }
        finally { File.Delete(path); }
    }

    private static byte[] Rectangle() => Join(N(100, 200), [21], N(400, 0, 0, 500, -400, 0), [5, 14]);
    private static byte[] Join(params byte[][] arrays) => arrays.SelectMany(array => array).ToArray();
    private static byte[] N(params int[] values) => values.SelectMany(value => new byte[] { 28, (byte)(value >> 8), (byte)value }).ToArray();
    private static byte[] D(int value) => [29, (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
    private static byte[] Index(params byte[][] objects)
    {
        if (objects.Length == 0) return [0, 0];
        var result = new List<byte> { (byte)(objects.Length >> 8), (byte)objects.Length, 2 };
        var offset = 1;
        foreach (var obj in objects)
        {
            result.Add((byte)(offset >> 8)); result.Add((byte)offset); offset += obj.Length;
        }
        result.Add((byte)(offset >> 8)); result.Add((byte)offset);
        result.AddRange(Join(objects));
        return result.ToArray();
    }

    private static byte[] Cff(byte[] glyph, byte[][]? local = null, byte[][]? global = null)
    {
        var prefix = Join([1, 0, 4, 4], Index(Encoding.ASCII.GetBytes("Synthetic")));
        var globalIndex = Index(global ?? []);
        var chars = Index([14], glyph);
        var privateDict = Join(D(6), [19]);
        byte[] Top(int charOffset, int privateOffset) => Join(D(charOffset), [17], D(privateDict.Length), D(privateOffset), [18]);
        var charOffset = prefix.Length + Index(Top(0, 0)).Length + 2 + globalIndex.Length;
        return Join(prefix, Index(Top(charOffset, charOffset + chars.Length)), [0, 0], globalIndex, chars, privateDict, Index(local ?? []));
    }

    private static byte[] CidCff(byte[] glyph, byte[] subroutine, bool ranges)
    {
        var prefix = Join([1, 0, 4, 4], Index(Encoding.ASCII.GetBytes("CIDTest")));
        var chars = Index([14], glyph);
        var select = ranges ? new byte[] { 3, 0, 2, 0, 0, 0, 0, 1, 1, 0, 2 } : new byte[] { 0, 0, 1 };
        byte[] Top(int charsOffset, int fdOffset, int selectOffset) => Join(D(0), D(0), D(0), [12, 30], D(charsOffset), [17], D(fdOffset), [12, 36], D(selectOffset), [12, 37]);
        byte[] Fd(int offset) => Join(D(6), D(offset), [18]);
        var charOffset = prefix.Length + Index(Top(0, 0, 0)).Length + 4;
        var fdOffset = charOffset + chars.Length;
        var selectOffset = fdOffset + Index([], Fd(0)).Length;
        var privateOffset = selectOffset + select.Length;
        return Join(prefix, Index(Top(charOffset, fdOffset, selectOffset)), [0, 0, 0, 0], chars, Index([], Fd(privateOffset)), select, D(6), [19], Index(subroutine));
    }

    private static byte[] Font(byte[] cff, string tag = "CFF ", ushort glyphCount = 2)
    {
        var head = new byte[54];
        BinaryPrimitives.WriteUInt32BigEndian(head, 0x10000);
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(12), 0x5F0F3CF5);
        BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(18), 1000);
        var maxp = new byte[6];
        BinaryPrimitives.WriteUInt32BigEndian(maxp, 0x5000);
        BinaryPrimitives.WriteUInt16BigEndian(maxp.AsSpan(4), glyphCount);
        var hhea = new byte[36];
        BinaryPrimitives.WriteUInt32BigEndian(hhea, 0x10000);
        BinaryPrimitives.WriteInt16BigEndian(hhea.AsSpan(4), 800);
        BinaryPrimitives.WriteInt16BigEndian(hhea.AsSpan(6), -200);
        BinaryPrimitives.WriteUInt16BigEndian(hhea.AsSpan(34), 2);
        var hmtx = new byte[] { 2, 88, 0, 0, 2, 88, 0, 0 };
        var cmap = new byte[40];
        BinaryPrimitives.WriteUInt16BigEndian(cmap.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16BigEndian(cmap.AsSpan(4), 3);
        BinaryPrimitives.WriteUInt16BigEndian(cmap.AsSpan(6), 10);
        BinaryPrimitives.WriteUInt32BigEndian(cmap.AsSpan(8), 12);
        BinaryPrimitives.WriteUInt16BigEndian(cmap.AsSpan(12), 12);
        BinaryPrimitives.WriteUInt32BigEndian(cmap.AsSpan(16), 28);
        BinaryPrimitives.WriteUInt32BigEndian(cmap.AsSpan(24), 1);
        BinaryPrimitives.WriteUInt32BigEndian(cmap.AsSpan(28), 'A');
        BinaryPrimitives.WriteUInt32BigEndian(cmap.AsSpan(32), 'A');
        BinaryPrimitives.WriteUInt32BigEndian(cmap.AsSpan(36), 1);
        (string Tag, byte[] Data)[] tables = [(tag, cff), ("head", head), ("maxp", maxp), ("hhea", hhea), ("hmtx", hmtx), ("cmap", cmap)];
        var position = 12 + 16 * tables.Length;
        var font = new byte[position + tables.Sum(table => table.Data.Length)];
        BinaryPrimitives.WriteUInt32BigEndian(font, 0x4F54544F);
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(4), (ushort)tables.Length);
        for (var i = 0; i < tables.Length; i++)
        {
            var record = font.AsSpan(12 + 16 * i, 16);
            Encoding.ASCII.GetBytes(tables[i].Tag, record[..4]);
            BinaryPrimitives.WriteUInt32BigEndian(record[8..], (uint)position);
            BinaryPrimitives.WriteUInt32BigEndian(record[12..], (uint)tables[i].Data.Length);
            tables[i].Data.CopyTo(font, position); position += tables[i].Data.Length;
        }
        return font;
    }
}
