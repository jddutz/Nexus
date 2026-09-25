using System.Buffers.Binary;
using System.Text;
using Nexus.Assets.Typography.FontReader;
using Nexus.Assets.Typography.FontReader.TrueType;
using Nexus.Assets.Typography.FontReader.TrueType.Tables;
using Nexus.Assets.Typography.Geometry;
using Xunit.Abstractions;

namespace Nexus.AssetPipeline.Tests;

/// <summary>
/// Tests bounded TrueType reads and SFNT table-directory parsing.
/// </summary>
public sealed class TrueTypeFontReaderTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes the test output writer.
    /// </summary>
    /// <param name="output">The test output writer.</param>
    public TrueTypeFontReaderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Verifies numeric reads use big-endian interpretation and advance the cursor.
    /// </summary>
    [Fact]
    public void TrueTypeReader_readsBigEndianValues()
    {
        var reader = new TrueTypeReader(
            new byte[]
            {
                0xAB,
                0x12,
                0x34,
                0xFF,
                0xFE,
                0x01,
                0x02,
                0x03,
                0x04,
                0xFF,
                0xFF,
                0xFF,
                0xFF,
            }
        );

        Assert.Equal((byte)0xAB, reader.ReadUInt8());
        Assert.Equal((ushort)0x1234, reader.ReadUInt16());
        Assert.Equal((short)-2, reader.ReadInt16());
        Assert.Equal(0x01020304u, reader.ReadUInt32());
        Assert.Equal(-1, reader.ReadInt32());
        Assert.Equal(13, reader.Position);
    }

    /// <summary>
    /// Verifies slices and cursor reads reject ranges beyond the input buffer.
    /// </summary>
    [Fact]
    public void TrueTypeReader_rejectsOutOfBoundsRanges()
    {
        var reader = new TrueTypeReader(new byte[] { 1, 2, 3 });

        Assert.Equal(new byte[] { 2, 3 }, reader.Slice(1, 2).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Slice(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Slice(0, int.MaxValue));
        Assert.Equal((byte)1, reader.ReadUInt8());
        Assert.Equal((ushort)0x0203, reader.ReadUInt16());
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadUInt8());
    }

    /// <summary>
    /// Verifies the SFNT directory preserves each table's tag, checksum, offset, and length.
    /// </summary>
    [Fact]
    public void TrueTypeFontReader_parsesTableDirectory()
    {
        var font = new TrueTypeFontReader(
            CreateFont(
                ("head", 0x11223344, 60u, 1u),
                ("cmap", 0x55667788, 61u, 1u),
                ("glyf", 0x99AABBCC, 62u, 1u)
            )
        );

        Assert.Equal(
            new[] { "head", "cmap", "glyf" },
            font.TableDirectory.Tables.Select(table => table.Tag)
        );
        Assert.True(font.TableDirectory.TryGetTable("head", out var head));
        Assert.Equal(0x11223344u, head.Checksum);
        Assert.Equal(60u, head.Offset);
        Assert.Equal(1u, head.Length);
        Assert.Equal(new byte[] { 60 }, font.GetTable("head").ToArray());
    }

    /// <summary>
    /// Verifies invalid signatures and table ranges are rejected.
    /// </summary>
    [Fact]
    public void TrueTypeFontReader_rejectsInvalidDirectoryData()
    {
        var invalidSignature = CreateFont(("head", 0u, 28u, 1u));
        invalidSignature[0] = 0xFF;

        Assert.Throws<InvalidDataException>(() => new TrueTypeFontReader(invalidSignature));
        Assert.Throws<InvalidDataException>(() =>
            new TrueTypeFontReader(CreateFont(("head", 0u, uint.MaxValue, 1u)))
        );
    }

    /// <summary>
    /// Verifies Unicode format 4 resolves BMP characters and missing characters to glyph zero.
    /// </summary>
    [Fact]
    public void TrueTypeFontReader_resolvesFormat4GlyphIndices()
    {
        var font = CreateFontWithTables(
            ("cmap", CreateCmap((3, 1, CreateFormat4Subtable()))),
            ("head", CreateHeadTable()),
            ("maxp", CreateMaxpTable(40)),
            ("hhea", CreateHheaTable(40))
        );

        var reader = new TrueTypeFontReader(font);

        Assert.Equal((ushort)37, reader.GetGlyphIndex('A'));
        Assert.Equal((ushort)38, reader.GetGlyphIndex('B'));
        Assert.Equal((ushort)4, reader.GetGlyphIndex(' '));
        Assert.Equal((ushort)0, reader.GetGlyphIndex('Z'));
        Assert.Equal((ushort)0, reader.GetGlyphIndex(-1));
        Assert.Equal((ushort)0, reader.GetGlyphIndex(0x110000));
    }

    /// <summary>
    /// Verifies Unicode format 12 resolves supplementary codepoints and takes precedence.
    /// </summary>
    [Fact]
    public void TrueTypeFontReader_resolvesFormat12GlyphIndices()
    {
        var format4 = CreateFormat4Subtable();
        var format12 = CreateFormat12Subtable(
            (0x20u, 0x20u, 4u),
            (0x41u, 0x41u, 37u),
            (0x42u, 0x42u, 38u),
            (0x1F600u, 0x1F600u, 7u)
        );
        var cmap = CreateCmap((0, 4, format12), (3, 1, format4));
        var font = new TrueTypeFontReader(
            CreateFontWithTables(
                ("cmap", cmap),
                ("head", CreateHeadTable()),
                ("maxp", CreateMaxpTable(40)),
                ("hhea", CreateHheaTable(40))
            )
        );

        Assert.Equal((ushort)37, font.GetGlyphIndex('A'));
        Assert.Equal((ushort)7, font.GetGlyphIndex(0x1F600));
        Assert.Equal((ushort)0, font.GetGlyphIndex(0x1F601));
    }

    /// <summary>
    /// Verifies codepoints resolve to their horizontal advances and bearings through cmap and hmtx.
    /// </summary>
    [Fact]
    public void TrueTypeFontReader_resolvesHorizontalMetricsForCodepoints()
    {
        var cmap = CreateCmap(
            (
                0,
                4,
                CreateFormat12Subtable(
                    (0x20u, 0x20u, 1u),
                    (0x41u, 0x41u, 2u),
                    (0x57u, 0x57u, 4u),
                    (0x69u, 0x69u, 3u)
                )
            )
        );
        var hmtx = CreateHmtxTable([(500, -10), (250, 12), (610, 20), (215, -3), (900, 1)]);
        var font = new TrueTypeFontReader(
            CreateFontWithTables(
                ("cmap", cmap),
                ("head", CreateHeadTable()),
                ("maxp", CreateMaxpTable(5)),
                ("hhea", CreateHheaTable(5)),
                ("hmtx", hmtx)
            )
        );

        Assert.Equal((ushort)250, font.GetHorizontalMetrics(font.GetGlyphIndex(' ')).AdvanceWidth);
        Assert.Equal((ushort)610, font.GetHorizontalMetrics(font.GetGlyphIndex('A')).AdvanceWidth);
        Assert.Equal((ushort)215, font.GetHorizontalMetrics(font.GetGlyphIndex('i')).AdvanceWidth);
        Assert.Equal((ushort)900, font.GetHorizontalMetrics(font.GetGlyphIndex('W')).AdvanceWidth);
        Assert.Equal((short)20, font.GetHorizontalMetrics(font.GetGlyphIndex('A')).LeftSideBearing);
    }

    /// <summary>
    /// Verifies repeated flags and signed coordinate deltas in both loca offset formats.
    /// </summary>
    /// <param name="indexToLocFormat">The short or long loca offset format.</param>
    [Theory]
    [InlineData((short)0)]
    [InlineData((short)1)]
    public void TrueTypeFontReader_decodesCompressedSimpleGlyphOutlines(short indexToLocFormat)
    {
        var glyf = CreateCompressedSimpleGlyph();
        var font = new TrueTypeFontReader(
            CreateFontWithTables(
                ("head", CreateHeadTable(indexToLocFormat)),
                ("maxp", CreateMaxpTable(1)),
                ("hhea", CreateHheaTable(1)),
                ("loca", CreateLocaTable(indexToLocFormat, glyf.Length)),
                ("glyf", glyf)
            )
        );

        var outline = font.GetGlyphOutline(0);

        var contour = Assert.Single(outline.Contours);
        Assert.Equal(4, contour.Points.Count);
        Assert.Equal(
            (0, 0, true),
            (contour.Points[0].X, contour.Points[0].Y, contour.Points[0].OnCurve)
        );
        Assert.Equal(
            (-3, -2, false),
            (contour.Points[1].X, contour.Points[1].Y, contour.Points[1].OnCurve)
        );
        Assert.Equal(
            (-7, -5, false),
            (contour.Points[2].X, contour.Points[2].Y, contour.Points[2].OnCurve)
        );
        Assert.Equal(
            (-2, 1, true),
            (contour.Points[3].X, contour.Points[3].Y, contour.Points[3].OnCurve)
        );
    }

    /// <summary>
    /// Verifies nested components apply their F2Dot14 scale and XY translation.
    /// </summary>
    [Fact]
    public void TrueTypeFontReader_resolvesNestedCompositeTransforms()
    {
        var font = new TrueTypeFontReader(
            CreateFontWithGlyphs(
                CreateCompressedSimpleGlyph(),
                CreateCompositeGlyph((0x000B, 0, 0, 0, [0x7FFF])),
                CreateCompositeGlyph((0x0003, 1, 10, 5, []))
            )
        );

        var outline = font.GetGlyphOutline(2);
        var point = Assert.Single(outline.Contours).Points[1];

        Assert.Equal((4, 1, false), (point.X, point.Y, point.OnCurve));
    }

    /// <summary>
    /// Verifies nested fractional transforms retain precision until the final outline coordinates are rounded.
    /// </summary>
    [Fact]
    public void TrueTypeFontReader_preservesPrecisionAcrossNestedFractionalTransforms()
    {
        var halfScale = new short[] { 8192 };
        var font = new TrueTypeFontReader(
            CreateFontWithGlyphs(
                CreateSinglePointGlyph(1, 1),
                CreateCompositeGlyph((0x000B, 0, 0, 0, halfScale)),
                CreateCompositeGlyph((0x000B, 1, 0, 0, halfScale))
            )
        );

        var point = Assert.Single(Assert.Single(font.GetGlyphOutline(2).Contours).Points);

        Assert.Equal((0, 0, true), (point.X, point.Y, point.OnCurve));
    }

    /// <summary>
    /// Verifies point-index arguments align a component point with an existing parent point.
    /// </summary>
    [Fact]
    public void TrueTypeFontReader_resolvesCompositePointAttachments()
    {
        var font = new TrueTypeFontReader(
            CreateFontWithGlyphs(
                CreateCompressedSimpleGlyph(),
                CreateCompositeGlyph((0x0023, 0, 100, 0, []), (0x0001, 0, 1, 0, []))
            )
        );

        var contours = font.GetGlyphOutline(1).Contours;

        Assert.Equal(2, contours.Count);
        Assert.Equal((-3 + 100, -2), (contours[1].Points[0].X, contours[1].Points[0].Y));
    }

    /// <summary>
    /// Verifies composite cycles, excessive nesting, and references outside the glyph set are rejected.
    /// </summary>
    [Fact]
    public void TrueTypeFontReader_rejectsMalformedCompositeGraphs()
    {
        var cyclicFont = new TrueTypeFontReader(
            CreateFontWithGlyphs(CreateCompositeGlyph((0x0003, 0, 0, 0, [])))
        );
        Assert.Throws<InvalidDataException>(() => cyclicFont.GetGlyphOutline(0));

        var deepGlyphs = Enumerable
            .Range(0, 32)
            .Select(index => CreateCompositeGlyph((0x0003, checked((ushort)(index + 1)), 0, 0, [])))
            .Append(CreateCompressedSimpleGlyph())
            .ToArray();
        var deepFont = new TrueTypeFontReader(CreateFontWithGlyphs(deepGlyphs));
        Assert.Throws<InvalidDataException>(() => deepFont.GetGlyphOutline(0));

        var invalidIndexFont = new TrueTypeFontReader(
            CreateFontWithGlyphs(CreateCompositeGlyph((0x0003, 1, 0, 0, [])))
        );
        Assert.Throws<InvalidDataException>(() => invalidIndexFont.GetGlyphOutline(0));
    }

    /// <summary>
    /// Verifies on-curve pairs become lines and an on/off/on sequence becomes a quadratic edge.
    /// </summary>
    [Fact]
    public void FontContourConverter_convertsLinesAndQuadratics()
    {
        var contour = FontContourConverter.Convert(
            new FontContour([
                new FontPoint(0, 0, true),
                new FontPoint(2, 3, false),
                new FontPoint(4, 0, true),
                new FontPoint(4, -2, true),
            ])
        );

        Assert.Collection(
            contour.Edges,
            edge =>
            {
                var quadratic = Assert.IsType<QuadraticSegment>(edge);
                Assert.Equal(new System.Numerics.Vector2(0, 0), quadratic.Start);
                Assert.Equal(new System.Numerics.Vector2(2, 3), quadratic.Control);
                Assert.Equal(new System.Numerics.Vector2(4, 0), quadratic.End);
            },
            edge => Assert.IsType<LineSegment>(edge),
            edge => Assert.IsType<LineSegment>(edge)
        );
    }

    /// <summary>
    /// Verifies adjacent off-curve points produce implied midpoint anchors, including across closure.
    /// </summary>
    [Fact]
    public void FontContourConverter_addsImpliedMidpointsAcrossContourWraparound()
    {
        var contour = FontContourConverter.Convert(
            new FontContour([
                new FontPoint(2, 0, false),
                new FontPoint(4, 0, true),
                new FontPoint(4, 2, false),
            ])
        );

        Assert.Collection(
            contour.Edges,
            edge =>
            {
                var quadratic = Assert.IsType<QuadraticSegment>(edge);
                Assert.Equal(new System.Numerics.Vector2(4, 0), quadratic.Start);
                Assert.Equal(new System.Numerics.Vector2(4, 2), quadratic.Control);
                Assert.Equal(new System.Numerics.Vector2(3, 1), quadratic.End);
            },
            edge =>
            {
                var quadratic = Assert.IsType<QuadraticSegment>(edge);
                Assert.Equal(new System.Numerics.Vector2(3, 1), quadratic.Start);
                Assert.Equal(new System.Numerics.Vector2(2, 0), quadratic.Control);
                Assert.Equal(new System.Numerics.Vector2(4, 0), quadratic.End);
            }
        );
    }

    /// <summary>
    /// Verifies adjacent off-curve points inside a contour share an implied on-curve midpoint.
    /// </summary>
    [Fact]
    public void FontContourConverter_addsImpliedMidpointsBetweenOffCurvePoints()
    {
        var contour = FontContourConverter.Convert(
            new FontContour([
                new FontPoint(0, 0, true),
                new FontPoint(2, 2, false),
                new FontPoint(4, 2, false),
                new FontPoint(6, 0, true),
            ])
        );

        var quadratics = contour.Edges.OfType<QuadraticSegment>().ToArray();
        Assert.Equal(2, quadratics.Length);
        Assert.Equal(new System.Numerics.Vector2(3, 2), quadratics[0].End);
        Assert.Equal(new System.Numerics.Vector2(3, 2), quadratics[1].Start);
    }

    /// <summary>
    /// Verifies conversion preserves the source on-curve vertices and off-curve control points.
    /// </summary>
    [Fact]
    public void Open_convertsLocalCurvesWithoutLosingTrueTypePointsWhenAvailable()
    {
        var fontPath = FindLocalFont("Roboto-Regular.ttf");
        if (fontPath is null)
            return;

        var font = TrueTypeFontReader.Open(fontPath);
        var outlines = new[]
        {
            font.GetGlyphOutline(font.GetGlyphIndex('O')),
            font.GetGlyphOutline(font.GetGlyphIndex('S')),
        };

        foreach (var sourceContour in outlines.SelectMany(outline => outline.Contours))
        {
            var converted = FontContourConverter.Convert(sourceContour);
            var endpoints = converted.Edges.SelectMany(edge => new[] { edge.Start, edge.End });
            var controls = converted.Edges.OfType<QuadraticSegment>().Select(edge => edge.Control);

            foreach (var point in sourceContour.Points)
            {
                var position = new System.Numerics.Vector2(point.X, point.Y);
                Assert.Contains(position, point.OnCurve ? endpoints : controls);
            }
        }
    }

    /// <summary>
    /// Dumps local-font I and O outlines and verifies the curved glyph contains off-curve points.
    /// </summary>
    [Fact]
    public void Open_dumpsLocalSimpleIAndOCurvesWhenAvailable()
    {
        var fontPath = FindLocalFont("Roboto-Regular.ttf");
        if (fontPath is null)
            return;

        var font = TrueTypeFontReader.Open(fontPath);
        var iGlyphIndex = font.GetGlyphIndex('I');
        var oGlyphIndex = font.GetGlyphIndex('O');
        var iOutline = font.GetGlyphOutline(iGlyphIndex);
        var oOutline = font.GetGlyphOutline(oGlyphIndex);

        Assert.NotEmpty(iOutline.Contours);
        Assert.True(oOutline.Contours.Count >= 2);
        Assert.Contains(
            oOutline.Contours.SelectMany(contour => contour.Points),
            point => !point.OnCurve
        );

        WriteOutline('I', iGlyphIndex, iOutline);
        WriteOutline('O', oGlyphIndex, oOutline);
    }

    /// <summary>
    /// Verifies common accented Latin glyphs resolve through composite outlines in the local font.
    /// </summary>
    [Fact]
    public void Open_resolvesLocalCompositeLatinAccentsWhenAvailable()
    {
        var fontPath = FindLocalFont("Roboto-Regular.ttf");
        if (fontPath is null)
            return;

        var font = TrueTypeFontReader.Open(fontPath);
        foreach (var character in new[] { 'é', 'Å' })
        {
            var outline = font.GetGlyphOutline(font.GetGlyphIndex(character));
            Assert.NotEmpty(outline.Contours);
        }
    }

    /// <summary>
    /// Verifies glyphs without full hmtx records reuse the last advance and keep their own bearings.
    /// </summary>
    [Fact]
    public void HmtxTable_reusesLastAdvanceForRemainingGlyphs()
    {
        var table = HmtxTable.Parse(
            new TrueTypeReader(CreateHmtxTable([(500, -10), (600, 20)], 25, -40)),
            glyphCount: 4,
            numberOfHorizontalMetrics: 2
        );

        Assert.Equal(new GlyphHorizontalMetrics(600, 25), table.GetMetrics(2));
        Assert.Equal(new GlyphHorizontalMetrics(600, -40), table.GetMetrics(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => table.GetMetrics(4));
    }

    /// <summary>
    /// Verifies a locally supplied TTF contains the expected core tables when available.
    /// </summary>
    [Fact]
    public void Open_readsTablesFromLocalTrueTypeFontWhenAvailable()
    {
        var fontPath = FindLocalFont("Roboto-Regular.ttf");
        if (fontPath is null)
            return;

        var font = TrueTypeFontReader.Open(fontPath);
        Assert.Contains(font.TableDirectory.Tables, table => table.Tag == "head");
        Assert.Contains(font.TableDirectory.Tables, table => table.Tag == "cmap");
        Assert.Contains(font.TableDirectory.Tables, table => table.Tag == "glyf");
        Assert.Equal((ushort)2048, font.FontFace.UnitsPerEm);
        Assert.Equal((short)1900, font.FontFace.Ascender);
        Assert.Equal((short)-500, font.FontFace.Descender);
        Assert.Equal((short)0, font.FontFace.LineGap);
        Assert.Equal((ushort)1321, font.FontFace.GlyphCount);
        Assert.Equal((ushort)1321, font.FontFace.NumberOfHorizontalMetrics);
        Assert.Equal((short)0, font.FontFace.IndexToLocFormat);
        Assert.Equal((ushort)37, font.GetGlyphIndex('A'));
        Assert.Equal((ushort)38, font.GetGlyphIndex('B'));
        Assert.Equal((ushort)4, font.GetGlyphIndex(' '));
        Assert.Equal((ushort)0, font.GetGlyphIndex(0x10FFFF));
        Assert.Equal((ushort)1336, font.GetHorizontalMetrics(font.GetGlyphIndex('A')).AdvanceWidth);
        Assert.Equal((ushort)498, font.GetHorizontalMetrics(font.GetGlyphIndex('i')).AdvanceWidth);
        Assert.Equal((ushort)1817, font.GetHorizontalMetrics(font.GetGlyphIndex('W')).AdvanceWidth);
        Assert.Equal((ushort)508, font.GetHorizontalMetrics(font.GetGlyphIndex(' ')).AdvanceWidth);
    }

    /// <summary>
    /// Verifies glyph resolution against a local font with Unicode format 12 subtables when available.
    /// </summary>
    [Fact]
    public void Open_resolvesGlyphsFromLocalFormat12FontWhenAvailable()
    {
        var fontPath = FindLocalFont("HomeVideo-BLG6G.ttf");
        if (fontPath is null)
            return;

        var font = TrueTypeFontReader.Open(fontPath);

        Assert.Equal((ushort)28, font.GetGlyphIndex('A'));
        Assert.Equal((ushort)29, font.GetGlyphIndex('B'));
        Assert.Equal((ushort)1, font.GetGlyphIndex(' '));
        Assert.Equal((ushort)0, font.GetGlyphIndex(0x10FFFF));
    }

    /// <summary>
    /// Verifies locally supplied OTF directories are parseable when available.
    /// </summary>
    [Fact]
    public void Open_readsTablesFromLocalOpenTypeFontsWhenAvailable()
    {
        var fontDirectory = FindLocalFontDirectory();
        if (fontDirectory is null)
            return;

        var fontPaths = Directory.GetFiles(fontDirectory, "*.otf");
        foreach (var fontPath in fontPaths)
        {
            var font = TrueTypeFontReader.Open(fontPath);
            Assert.Contains(font.TableDirectory.Tables, table => table.Tag == "head");
            Assert.Contains(font.TableDirectory.Tables, table => table.Tag == "cmap");
        }
    }

    /// <summary>
    /// Creates a minimal SFNT byte buffer containing the requested table records.
    /// </summary>
    /// <param name="records">The records to include in the table directory.</param>
    /// <returns>A font buffer with one data byte for each table.</returns>
    private static byte[] CreateFont(
        params (string Tag, uint Checksum, uint Offset, uint Length)[] records
    )
    {
        const int headerLength = 12;
        const int recordLength = 16;
        var dataOffset = headerLength + records.Length * recordLength;
        var font = new byte[dataOffset + records.Length];
        BinaryPrimitives.WriteUInt32BigEndian(font, 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(4), checked((ushort)records.Length));

        for (var index = 0; index < records.Length; index++)
        {
            var record = records[index];
            var recordOffset = headerLength + index * recordLength;
            Encoding.ASCII.GetBytes(record.Tag, font.AsSpan(recordOffset, 4));
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(recordOffset + 4), record.Checksum);
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(recordOffset + 8), record.Offset);
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(recordOffset + 12), record.Length);

            var tableDataIndex = dataOffset + index;
            if (record.Offset == tableDataIndex && record.Length > 0)
                font[tableDataIndex] = (byte)tableDataIndex;
        }

        return font;
    }

    /// <summary>
    /// Creates an SFNT containing the supplied table bytes.
    /// </summary>
    /// <param name="tables">The table tags and contents to include.</param>
    /// <returns>A complete SFNT byte buffer.</returns>
    private static byte[] CreateFontWithTables(params (string Tag, byte[] Data)[] tables)
    {
        const int headerLength = 12;
        const int recordLength = 16;
        var dataOffset = headerLength + tables.Length * recordLength;
        var font = new byte[dataOffset + tables.Sum(table => table.Data.Length)];
        BinaryPrimitives.WriteUInt32BigEndian(font, 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(4), checked((ushort)tables.Length));

        var tablePosition = dataOffset;
        for (var index = 0; index < tables.Length; index++)
        {
            var table = tables[index];
            var recordOffset = headerLength + index * recordLength;
            Encoding.ASCII.GetBytes(table.Tag, font.AsSpan(recordOffset, 4));
            BinaryPrimitives.WriteUInt32BigEndian(
                font.AsSpan(recordOffset + 8),
                checked((uint)tablePosition)
            );
            BinaryPrimitives.WriteUInt32BigEndian(
                font.AsSpan(recordOffset + 12),
                checked((uint)table.Data.Length)
            );
            table.Data.CopyTo(font, tablePosition);
            tablePosition += table.Data.Length;
        }

        return font;
    }

    /// <summary>
    /// Creates a cmap header and its encoding records.
    /// </summary>
    /// <param name="subtables">The platform, encoding, and subtable tuples.</param>
    /// <returns>The cmap table bytes.</returns>
    private static byte[] CreateCmap(
        params (ushort Platform, ushort Encoding, byte[] Data)[] subtables
    )
    {
        var headerLength = 4 + subtables.Length * 8;
        var cmap = new byte[headerLength + subtables.Sum(subtable => subtable.Data.Length)];
        BinaryPrimitives.WriteUInt16BigEndian(cmap.AsSpan(2), checked((ushort)subtables.Length));
        var subtablePosition = headerLength;
        for (var index = 0; index < subtables.Length; index++)
        {
            var subtable = subtables[index];
            var recordOffset = 4 + index * 8;
            BinaryPrimitives.WriteUInt16BigEndian(cmap.AsSpan(recordOffset), subtable.Platform);
            BinaryPrimitives.WriteUInt16BigEndian(cmap.AsSpan(recordOffset + 2), subtable.Encoding);
            BinaryPrimitives.WriteUInt32BigEndian(
                cmap.AsSpan(recordOffset + 4),
                checked((uint)subtablePosition)
            );
            subtable.Data.CopyTo(cmap, subtablePosition);
            subtablePosition += subtable.Data.Length;
        }

        return cmap;
    }

    /// <summary>
    /// Creates a format 4 map for space, A, B, and the required terminal segment.
    /// </summary>
    /// <returns>The format 4 subtable bytes.</returns>
    private static byte[] CreateFormat4Subtable()
    {
        ushort[] codepoints = [0x20, 0x41, 0x42, 0xFFFF];
        ushort[] glyphs = [4, 37, 38, 0];
        var subtable = new byte[16 + codepoints.Length * 8];
        BinaryPrimitives.WriteUInt16BigEndian(subtable, 4);
        BinaryPrimitives.WriteUInt16BigEndian(subtable.AsSpan(2), checked((ushort)subtable.Length));
        BinaryPrimitives.WriteUInt16BigEndian(
            subtable.AsSpan(6),
            checked((ushort)(codepoints.Length * 2))
        );
        var position = 14;
        foreach (var codepoint in codepoints)
        {
            BinaryPrimitives.WriteUInt16BigEndian(subtable.AsSpan(position), codepoint);
            position += 2;
        }

        position += 2;
        foreach (var codepoint in codepoints)
        {
            BinaryPrimitives.WriteUInt16BigEndian(subtable.AsSpan(position), codepoint);
            position += 2;
        }

        for (var index = 0; index < codepoints.Length; index++)
        {
            var delta = unchecked((short)(glyphs[index] - codepoints[index]));
            BinaryPrimitives.WriteInt16BigEndian(subtable.AsSpan(position), delta);
            position += 2;
        }

        return subtable;
    }

    /// <summary>
    /// Creates a format 12 map from sequential codepoint/glyph groups.
    /// </summary>
    /// <param name="groups">The start codepoint, end codepoint, and start glyph tuples.</param>
    /// <returns>The format 12 subtable bytes.</returns>
    private static byte[] CreateFormat12Subtable(params (uint Start, uint End, uint Glyph)[] groups)
    {
        var subtable = new byte[16 + groups.Length * 12];
        BinaryPrimitives.WriteUInt16BigEndian(subtable, 12);
        BinaryPrimitives.WriteUInt32BigEndian(subtable.AsSpan(4), checked((uint)subtable.Length));
        BinaryPrimitives.WriteUInt32BigEndian(subtable.AsSpan(12), checked((uint)groups.Length));
        for (var index = 0; index < groups.Length; index++)
        {
            var group = groups[index];
            var position = 16 + index * 12;
            BinaryPrimitives.WriteUInt32BigEndian(subtable.AsSpan(position), group.Start);
            BinaryPrimitives.WriteUInt32BigEndian(subtable.AsSpan(position + 4), group.End);
            BinaryPrimitives.WriteUInt32BigEndian(subtable.AsSpan(position + 8), group.Glyph);
        }

        return subtable;
    }

    /// <summary>
    /// Creates a minimal maximum-profile table with the requested glyph count.
    /// </summary>
    /// <param name="glyphCount">The number of glyphs in the test font.</param>
    /// <returns>The maxp table bytes.</returns>
    private static byte[] CreateMaxpTable(ushort glyphCount)
    {
        var table = new byte[6];
        BinaryPrimitives.WriteUInt32BigEndian(table, 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(4), glyphCount);
        return table;
    }

    /// <summary>
    /// Creates a horizontal header table with the requested full metric count.
    /// </summary>
    /// <param name="numberOfHorizontalMetrics">The number of full hmtx records.</param>
    /// <returns>The hhea table bytes.</returns>
    private static byte[] CreateHheaTable(ushort numberOfHorizontalMetrics)
    {
        var table = new byte[36];
        BinaryPrimitives.WriteUInt32BigEndian(table, 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(34), numberOfHorizontalMetrics);
        return table;
    }

    /// <summary>
    /// Creates the minimal valid head table needed to initialize a font face.
    /// </summary>
    /// <returns>The head table bytes.</returns>
    private static byte[] CreateHeadTable(short indexToLocFormat = 0)
    {
        var table = new byte[54];
        BinaryPrimitives.WriteUInt32BigEndian(table, 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(table.AsSpan(12), 0x5F0F3CF5);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(18), 1000);
        BinaryPrimitives.WriteInt16BigEndian(table.AsSpan(50), indexToLocFormat);
        return table;
    }

    /// <summary>
    /// Creates a four-point simple glyph with one repeated flag run and signed short deltas.
    /// </summary>
    /// <returns>The encoded glyf record padded to an even byte length.</returns>
    private static byte[] CreateCompressedSimpleGlyph()
    {
        var glyph = new byte[26];
        BinaryPrimitives.WriteInt16BigEndian(glyph, 1);
        BinaryPrimitives.WriteUInt16BigEndian(glyph.AsSpan(10), 3);
        BinaryPrimitives.WriteUInt16BigEndian(glyph.AsSpan(12), 0);
        glyph[14] = 0x31;
        glyph[15] = 0x0E;
        glyph[16] = 1;
        glyph[17] = 0x37;
        glyph[18] = 3;
        glyph[19] = 4;
        glyph[20] = 5;
        glyph[21] = 2;
        glyph[22] = 3;
        glyph[23] = 6;
        return glyph;
    }

    /// <summary>
    /// Creates a one-point simple glyph at the requested coordinates.
    /// </summary>
    /// <param name="x">The point's horizontal coordinate.</param>
    /// <param name="y">The point's vertical coordinate.</param>
    /// <returns>The encoded simple glyph padded to an even byte length.</returns>
    private static byte[] CreateSinglePointGlyph(byte x, byte y)
    {
        var glyph = new byte[18];
        BinaryPrimitives.WriteInt16BigEndian(glyph, 1);
        glyph[14] = 0x37;
        glyph[15] = x;
        glyph[16] = y;
        return glyph;
    }

    /// <summary>
    /// Creates a composite glyph with the supplied component records.
    /// </summary>
    /// <param name="components">The flags, glyph index, arguments, and encoded transform values.</param>
    /// <returns>The encoded composite glyph.</returns>
    private static byte[] CreateCompositeGlyph(
        params (
            ushort Flags,
            ushort GlyphIndex,
            short Argument1,
            short Argument2,
            short[] Transform
        )[] components
    )
    {
        var length = 10;
        foreach (var component in components)
        {
            length += (component.Flags & 1) != 0 ? 8 : 6;
            length += component.Transform.Length * 2;
        }

        var glyph = new byte[length];
        BinaryPrimitives.WriteInt16BigEndian(glyph, -1);
        var position = 10;
        foreach (var component in components)
        {
            BinaryPrimitives.WriteUInt16BigEndian(glyph.AsSpan(position), component.Flags);
            BinaryPrimitives.WriteUInt16BigEndian(glyph.AsSpan(position + 2), component.GlyphIndex);
            position += 4;
            if ((component.Flags & 1) != 0)
            {
                BinaryPrimitives.WriteInt16BigEndian(glyph.AsSpan(position), component.Argument1);
                BinaryPrimitives.WriteInt16BigEndian(
                    glyph.AsSpan(position + 2),
                    component.Argument2
                );
                position += 4;
            }
            else
            {
                glyph[position++] = unchecked((byte)(sbyte)component.Argument1);
                glyph[position++] = unchecked((byte)(sbyte)component.Argument2);
            }

            foreach (var value in component.Transform)
            {
                BinaryPrimitives.WriteInt16BigEndian(glyph.AsSpan(position), value);
                position += 2;
            }
        }

        return glyph;
    }

    /// <summary>
    /// Creates a font containing a long-format loca table and the requested glyphs.
    /// </summary>
    /// <param name="glyphs">The glyph records in glyph-index order.</param>
    /// <returns>The complete SFNT font bytes.</returns>
    private static byte[] CreateFontWithGlyphs(params byte[][] glyphs)
    {
        var glyf = new byte[glyphs.Sum(glyph => glyph.Length)];
        var loca = new byte[(glyphs.Length + 1) * sizeof(uint)];
        var offset = 0;
        for (var index = 0; index < glyphs.Length; index++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(
                loca.AsSpan(index * sizeof(uint)),
                checked((uint)offset)
            );
            glyphs[index].CopyTo(glyf, offset);
            offset += glyphs[index].Length;
        }

        BinaryPrimitives.WriteUInt32BigEndian(
            loca.AsSpan(glyphs.Length * sizeof(uint)),
            checked((uint)offset)
        );
        return CreateFontWithTables(
            ("head", CreateHeadTable(1)),
            ("maxp", CreateMaxpTable(checked((ushort)glyphs.Length))),
            ("hhea", CreateHheaTable(checked((ushort)glyphs.Length))),
            ("loca", loca),
            ("glyf", glyf)
        );
    }

    /// <summary>
    /// Creates the loca offsets for one glyph in the selected offset format.
    /// </summary>
    /// <param name="indexToLocFormat">The short or long loca offset format.</param>
    /// <param name="glyfLength">The byte length of the glyph data.</param>
    /// <returns>The two loca offsets delimiting the glyph.</returns>
    private static byte[] CreateLocaTable(short indexToLocFormat, int glyfLength)
    {
        if (indexToLocFormat == 0)
        {
            var table = new byte[4];
            BinaryPrimitives.WriteUInt16BigEndian(
                table.AsSpan(2),
                checked((ushort)(glyfLength / 2))
            );
            return table;
        }

        var longTable = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(longTable.AsSpan(4), checked((uint)glyfLength));
        return longTable;
    }

    /// <summary>
    /// Writes each contour's decoded points to the test output.
    /// </summary>
    /// <param name="character">The character used to resolve the glyph.</param>
    /// <param name="glyphIndex">The glyph's index in the font.</param>
    /// <param name="outline">The decoded outline to dump.</param>
    private void WriteOutline(char character, ushort glyphIndex, FontGlyphOutline outline)
    {
        _output.WriteLine($"{character} glyph {glyphIndex}");
        for (var contourIndex = 0; contourIndex < outline.Contours.Count; contourIndex++)
        {
            var points = string.Join(
                " ",
                outline
                    .Contours[contourIndex]
                    .Points.Select(point =>
                        $"({point.X},{point.Y},{(point.OnCurve ? "on" : "off")})"
                    )
            );
            _output.WriteLine($"  contour {contourIndex}: {points}");
        }
    }

    /// <summary>
    /// Creates hmtx bytes from full records followed by optional bearing-only entries.
    /// </summary>
    /// <param name="metrics">The advance and bearing pairs in full records.</param>
    /// <param name="remainingBearings">The bearings for glyphs after the full records.</param>
    /// <returns>The hmtx table bytes.</returns>
    private static byte[] CreateHmtxTable(
        (ushort AdvanceWidth, short LeftSideBearing)[] metrics,
        params short[] remainingBearings
    )
    {
        var table = new byte[metrics.Length * 4 + remainingBearings.Length * 2];
        for (var index = 0; index < metrics.Length; index++)
        {
            var position = index * 4;
            BinaryPrimitives.WriteUInt16BigEndian(
                table.AsSpan(position),
                metrics[index].AdvanceWidth
            );
            BinaryPrimitives.WriteInt16BigEndian(
                table.AsSpan(position + 2),
                metrics[index].LeftSideBearing
            );
        }

        var bearingPosition = metrics.Length * 4;
        for (var index = 0; index < remainingBearings.Length; index++)
            BinaryPrimitives.WriteInt16BigEndian(
                table.AsSpan(bearingPosition + index * 2),
                remainingBearings[index]
            );

        return table;
    }

    /// <summary>
    /// Finds a local font asset by walking upward from the test output directory.
    /// </summary>
    /// <param name="fileName">The font file to find in the local asset folder.</param>
    /// <returns>The full font path, or null when local font assets are unavailable.</returns>
    private static string? FindLocalFont(string fileName)
    {
        var fontDirectory = FindLocalFontDirectory();
        var path = fontDirectory is null ? null : Path.Combine(fontDirectory, fileName);
        return path is not null && File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Finds the repository's ignored local font-assets folder, if present.
    /// </summary>
    /// <returns>The font-assets directory, or null when it is unavailable.</returns>
    private static string? FindLocalFontDirectory()
    {
        for (
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent
        )
        {
            var candidate = Path.Combine(directory.FullName, ".assets", "Fonts");
            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
