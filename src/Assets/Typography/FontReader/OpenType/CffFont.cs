using Nexus.Assets.Typography.Geometry;

namespace Nexus.Assets.Typography.FontReader.OpenType;

/// <summary>Parses the outline and subroutine structures of a CFF 1 font.</summary>
internal sealed class CffFont
{
    private readonly ReadOnlyMemory<byte>[] _charStrings;
    private readonly ReadOnlyMemory<byte>[] _globalSubrs;
    private readonly ReadOnlyMemory<byte>[][] _localSubrs;
    private readonly int[] _fontIndices;

    internal CffFont(ReadOnlyMemory<byte> data, ushort glyphCount)
    {
        var reader = new CffDataReader(data);
        if (reader.Byte() != 1) throw new InvalidDataException("Only CFF version 1 is supported.");
        _ = reader.Byte();
        var headerSize = reader.Byte();
        var offsetSize = reader.Byte();
        if (headerSize < 4 || headerSize > data.Length || offsetSize is < 1 or > 4)
            throw new InvalidDataException("Invalid CFF header.");
        reader = new CffDataReader(data, headerSize);
        if (reader.Index().Length != 1) throw new InvalidDataException("OpenType CFF must contain one font.");
        var topDicts = reader.Index();
        if (topDicts.Length != 1) throw new InvalidDataException("OpenType CFF must contain one Top DICT.");
        var top = CffDataReader.Dict(topDicts[0]);
        _ = reader.Index(); // String INDEX: glyph mapping comes from cmap.
        _globalSubrs = reader.Index();
        ValidateDict(top);
        _charStrings = new CffDataReader(data, Offset(top, 17)).Index();
        if (_charStrings.Length != glyphCount) throw new InvalidDataException("CFF CharStrings count differs from maxp.");
        _fontIndices = new int[glyphCount];
        if (top.ContainsKey(1230)) // ROS: CID-keyed font
        {
            var fonts = new CffDataReader(data, Offset(top, 1236)).Index();
            if (fonts.Length is < 1 or > 256) throw new InvalidDataException("Invalid CFF FDArray count.");
            _localSubrs = fonts.Select(font => ReadPrivate(data, CffDataReader.Dict(font))).ToArray();
            ReadFontIndices(new CffDataReader(data, Offset(top, 1237)), fonts.Length);
        }
        else _localSubrs = [ReadPrivate(data, top)];
    }

    internal IReadOnlyList<Contour> GetGlyphContours(ushort glyphIndex, float cubicApproximationTolerance) =>
        new Type2CharString(_globalSubrs, _localSubrs[_fontIndices[glyphIndex]], cubicApproximationTolerance)
            .Decode(_charStrings[glyphIndex]);

    private static int Offset(Dictionary<int, double[]> dict, int op)
    {
        if (!dict.TryGetValue(op, out var values) || values.Length != 1)
            throw new InvalidDataException($"Missing or invalid CFF DICT operator {op}.");
        return CffDataReader.Integer(values[0]);
    }

    private static void ValidateDict(Dictionary<int, double[]> dict)
    {
        if (dict.TryGetValue(1206, out var type) && (type.Length != 1 || type[0] != 2))
            throw new InvalidDataException("Only CFF Type 2 charstrings are supported.");
        // OpenType requires the default matrix. Reject other transforms rather than mis-scaling outlines.
        if (dict.TryGetValue(1207, out var matrix)
            && !matrix.SequenceEqual(new double[] { .001, 0, 0, .001, 0, 0 }))
            throw new InvalidDataException("Non-default CFF FontMatrix transforms are not supported.");
    }

    private static ReadOnlyMemory<byte>[] ReadPrivate(ReadOnlyMemory<byte> data, Dictionary<int, double[]> dict)
    {
        ValidateDict(dict);
        if (!dict.TryGetValue(18, out var values)) return [];
        if (values.Length != 2) throw new InvalidDataException("Invalid CFF Private DICT reference.");
        var size = CffDataReader.Integer(values[0]);
        var offset = CffDataReader.Integer(values[1]);
        var reader = new CffDataReader(data);
        var privateDict = CffDataReader.Dict(reader.Slice(offset, size));
        if (!privateDict.ContainsKey(19)) return [];
        var subrsOffset = Offset(privateDict, 19);
        if (subrsOffset > data.Length - offset)
            throw new InvalidDataException("CFF local subroutine offset exceeds the table.");
        return new CffDataReader(data, offset + subrsOffset).Index();
    }

    private void ReadFontIndices(CffDataReader reader, int fontCount)
    {
        var format = reader.Byte();
        if (format == 0)
        {
            for (var i = 0; i < _fontIndices.Length; i++) _fontIndices[i] = reader.Byte();
        }
        else if (format == 3)
        {
            var ranges = reader.Unsigned(2);
            if (ranges == 0) throw new InvalidDataException("Empty CFF FDSelect ranges.");
            var start = reader.Unsigned(2);
            if (start != 0) throw new InvalidDataException("CFF FDSelect must start at glyph zero.");
            for (var i = 0; i < ranges; i++)
            {
                var font = reader.Byte();
                var end = reader.Unsigned(2);
                if (end <= start || end > _fontIndices.Length) throw new InvalidDataException("Invalid CFF FDSelect range.");
                Array.Fill(_fontIndices, font, start, end - start);
                start = end;
            }
            if (start != _fontIndices.Length) throw new InvalidDataException("Invalid CFF FDSelect sentinel.");
        }
        else throw new InvalidDataException("Unsupported CFF FDSelect format.");
        if (_fontIndices.Any(index => index >= fontCount)) throw new InvalidDataException("CFF FDSelect references a missing font.");
    }
}
