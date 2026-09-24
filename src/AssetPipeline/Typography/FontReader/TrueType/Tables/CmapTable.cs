using Nexus.AssetPipeline.Typography.FontReader.TrueType;

namespace Nexus.AssetPipeline.Typography.FontReader.TrueType.Tables;

/// <summary>
/// Resolves Unicode codepoints using supported character-map subtables.
/// </summary>
public sealed class CmapTable
{
    private const uint MaximumUnicodeCodepoint = 0x10FFFF;
    private readonly Format4Subtable? _format4;
    private readonly Format12Subtable? _format12;

    /// <summary>
    /// Initializes the selected Unicode character maps.
    /// </summary>
    /// <param name="format4">The selected format 4 map, when present.</param>
    /// <param name="format12">The selected format 12 map, when present.</param>
    private CmapTable(Format4Subtable? format4, Format12Subtable? format12)
    {
        _format4 = format4;
        _format12 = format12;
    }

    /// <summary>
    /// Parses supported Unicode format 4 and format 12 subtables.
    /// </summary>
    /// <param name="reader">A reader bounded to the cmap table data.</param>
    /// <param name="glyphCount">The number of glyphs declared by the font.</param>
    /// <returns>The parsed character map.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="InvalidDataException">The table is malformed or has no supported Unicode map.</exception>
    public static CmapTable Parse(TrueTypeReader reader, ushort glyphCount)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (reader.Length < 4)
            throw new InvalidDataException("The 'cmap' table is too short.");

        if (reader.ReadUInt16() != 0)
            throw new InvalidDataException("The 'cmap' table has an unsupported version.");

        var recordCount = reader.ReadUInt16();
        var format4Candidates = new List<CmapCandidate>();
        var format12Candidates = new List<CmapCandidate>();
        for (var index = 0; index < recordCount; index++)
        {
            var platformId = reader.ReadUInt16();
            var encodingId = reader.ReadUInt16();
            var offset = reader.ReadUInt32();
            if (!IsUnicodeEncoding(platformId, encodingId))
                continue;

            if (offset > int.MaxValue || offset > (uint)(reader.Length - 2))
                throw new InvalidDataException("A 'cmap' encoding record has an invalid offset.");

            var formatReader = new TrueTypeReader(reader.Slice((int)offset, 2));
            var format = formatReader.ReadUInt16();
            var candidate = new CmapCandidate(platformId, encodingId, offset);
            if (format == 4)
                format4Candidates.Add(candidate);
            else if (format == 12)
                format12Candidates.Add(candidate);
        }

        var format4Candidate = FindBestCandidate(format4Candidates);
        var format12Candidate = FindBestCandidate(format12Candidates);
        if (format4Candidate is null && format12Candidate is null)
            throw new InvalidDataException(
                "The 'cmap' table has no supported Unicode format 4 or format 12 subtable."
            );

        var format4 = format4Candidate is null
            ? null
            : ParseFormat4(
                reader.Slice(
                    (int)format4Candidate.Offset,
                    reader.Length - (int)format4Candidate.Offset
                ),
                glyphCount
            );
        var format12 = format12Candidate is null
            ? null
            : ParseFormat12(
                reader.Slice(
                    (int)format12Candidate.Offset,
                    reader.Length - (int)format12Candidate.Offset
                ),
                glyphCount
            );
        return new CmapTable(format4, format12);
    }

    /// <summary>
    /// Gets the glyph index for a Unicode codepoint, or glyph zero when it is absent.
    /// </summary>
    /// <param name="codepoint">The Unicode codepoint to resolve.</param>
    /// <returns>The glyph index, or zero if no mapping exists.</returns>
    public ushort GetGlyphIndex(int codepoint)
    {
        if (codepoint < 0 || (uint)codepoint > MaximumUnicodeCodepoint)
            return 0;

        if (
            _format12 is not null
            && _format12.TryGetGlyphIndex((uint)codepoint, out var glyphIndex)
        )
            return glyphIndex;

        return _format4 is not null && codepoint <= ushort.MaxValue
            ? _format4.GetGlyphIndex((ushort)codepoint)
            : (ushort)0;
    }

    /// <summary>
    /// Selects the preferred supported subtable, favoring Unicode-platform records.
    /// </summary>
    /// <param name="candidates">The candidate records for one cmap format.</param>
    /// <returns>The preferred candidate, or null when no candidate exists.</returns>
    private static CmapCandidate? FindBestCandidate(List<CmapCandidate> candidates)
    {
        if (candidates.Count == 0)
            return null;

        candidates.Sort(
            static (left, right) => GetEncodingPriority(left).CompareTo(GetEncodingPriority(right))
        );
        return candidates[0];
    }

    /// <summary>
    /// Gets the priority of a Unicode platform and encoding record.
    /// </summary>
    /// <param name="candidate">The cmap encoding record.</param>
    /// <returns>A lower value for a more preferred record.</returns>
    private static int GetEncodingPriority(CmapCandidate candidate) =>
        candidate.PlatformId == 0
            ? candidate.EncodingId == 4
                ? 0
                : 1
            : candidate.EncodingId == 10
                ? 2
                : 3;

    /// <summary>
    /// Determines whether an encoding record identifies Unicode text.
    /// </summary>
    /// <param name="platformId">The cmap platform identifier.</param>
    /// <param name="encodingId">The cmap encoding identifier.</param>
    /// <returns><see langword="true"/> for a Unicode platform or Microsoft Unicode encoding.</returns>
    private static bool IsUnicodeEncoding(ushort platformId, ushort encodingId) =>
        platformId == 0 || platformId == 3 && encodingId is 1 or 10;

    /// <summary>
    /// Parses a format 4 BMP character map.
    /// </summary>
    /// <param name="data">The bytes beginning at the format 4 subtable.</param>
    /// <param name="glyphCount">The number of glyphs declared by the font.</param>
    /// <returns>The parsed format 4 map.</returns>
    /// <exception cref="InvalidDataException">The format 4 subtable is malformed.</exception>
    private static Format4Subtable ParseFormat4(ReadOnlyMemory<byte> data, ushort glyphCount)
    {
        var header = new TrueTypeReader(data);
        if (header.Length < 4 || header.ReadUInt16() != 4)
            throw new InvalidDataException("The 'cmap' format 4 subtable has an invalid header.");

        var length = header.ReadUInt16();
        if (length > data.Length || length < 16)
            throw new InvalidDataException("The 'cmap' format 4 subtable has an invalid length.");

        var reader = new TrueTypeReader(data.Slice(0, length));
        _ = reader.ReadUInt16();
        _ = reader.ReadUInt16();
        _ = reader.ReadUInt16();
        var segmentCountX2 = reader.ReadUInt16();
        if (segmentCountX2 == 0 || (segmentCountX2 & 1) != 0)
            throw new InvalidDataException(
                "The 'cmap' format 4 subtable has an invalid segment count."
            );

        var segmentCount = segmentCountX2 / 2;
        if (16 + segmentCount * 8 > length)
            throw new InvalidDataException("The 'cmap' format 4 subtable is truncated.");

        _ = reader.ReadUInt16();
        _ = reader.ReadUInt16();
        _ = reader.ReadUInt16();
        var endCodes = new ushort[segmentCount];
        for (var index = 0; index < segmentCount; index++)
            endCodes[index] = reader.ReadUInt16();

        _ = reader.ReadUInt16();
        var startCodes = new ushort[segmentCount];
        for (var index = 0; index < segmentCount; index++)
            startCodes[index] = reader.ReadUInt16();

        var idDeltas = new short[segmentCount];
        for (var index = 0; index < segmentCount; index++)
            idDeltas[index] = reader.ReadInt16();

        var idRangeOffsetsPosition = reader.Position;
        var idRangeOffsets = new ushort[segmentCount];
        for (var index = 0; index < segmentCount; index++)
            idRangeOffsets[index] = reader.ReadUInt16();

        for (var index = 0; index < segmentCount; index++)
        {
            if (
                startCodes[index] > endCodes[index]
                || index > 0 && endCodes[index - 1] > endCodes[index]
            )
                throw new InvalidDataException(
                    "The 'cmap' format 4 subtable has invalid segments."
                );

            if (idRangeOffsets[index] == 0)
                continue;

            var lastGlyphOffset =
                idRangeOffsetsPosition
                + index * sizeof(ushort)
                + idRangeOffsets[index]
                + (endCodes[index] - startCodes[index]) * sizeof(ushort);
            if (lastGlyphOffset < 0 || lastGlyphOffset > length - sizeof(ushort))
                throw new InvalidDataException(
                    "The 'cmap' format 4 subtable has an invalid glyph offset."
                );
        }

        return new Format4Subtable(
            data.Slice(0, length),
            startCodes,
            endCodes,
            idDeltas,
            idRangeOffsets,
            idRangeOffsetsPosition,
            glyphCount
        );
    }

    /// <summary>
    /// Parses a format 12 full-repertoire character map.
    /// </summary>
    /// <param name="data">The bytes beginning at the format 12 subtable.</param>
    /// <param name="glyphCount">The number of glyphs declared by the font.</param>
    /// <returns>The parsed format 12 map.</returns>
    /// <exception cref="InvalidDataException">The format 12 subtable is malformed.</exception>
    private static Format12Subtable ParseFormat12(ReadOnlyMemory<byte> data, ushort glyphCount)
    {
        var header = new TrueTypeReader(data);
        if (header.Length < 16 || header.ReadUInt16() != 12)
            throw new InvalidDataException("The 'cmap' format 12 subtable has an invalid header.");

        if (header.ReadUInt16() != 0)
            throw new InvalidDataException(
                "The 'cmap' format 12 subtable has an invalid reserved field."
            );

        var length = header.ReadUInt32();
        _ = header.ReadUInt32();
        var groupCount = header.ReadUInt32();
        if (length > data.Length || length < 16 || groupCount > (length - 16) / 12)
            throw new InvalidDataException("The 'cmap' format 12 subtable has an invalid length.");

        var reader = new TrueTypeReader(data.Slice(0, (int)length));
        _ = reader.ReadUInt16();
        _ = reader.ReadUInt16();
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();

        var groups = new Format12Group[groupCount];
        uint previousEnd = 0;
        for (var index = 0; index < groups.Length; index++)
        {
            var startCodepoint = reader.ReadUInt32();
            var endCodepoint = reader.ReadUInt32();
            var startGlyphIndex = reader.ReadUInt32();
            var lastGlyphIndex = (ulong)startGlyphIndex + endCodepoint - startCodepoint;
            if (
                startCodepoint > endCodepoint
                || endCodepoint > MaximumUnicodeCodepoint
                || index > 0 && startCodepoint <= previousEnd
                || lastGlyphIndex >= glyphCount
                || lastGlyphIndex > ushort.MaxValue
            )
            {
                throw new InvalidDataException(
                    $"The 'cmap' format 12 subtable has an invalid group at index {index}: "
                        + $"U+{startCodepoint:X}-U+{endCodepoint:X}, glyph {startGlyphIndex}, "
                        + $"font glyph count {glyphCount}."
                );
            }

            groups[index] = new Format12Group(startCodepoint, endCodepoint, startGlyphIndex);
            previousEnd = endCodepoint;
        }

        return new Format12Subtable(groups);
    }

    /// <summary>
    /// Describes a Unicode cmap encoding record selected for parsing.
    /// </summary>
    /// <param name="PlatformId">The cmap platform identifier.</param>
    /// <param name="EncodingId">The cmap encoding identifier.</param>
    /// <param name="Offset">The subtable offset relative to the cmap table.</param>
    private sealed record CmapCandidate(ushort PlatformId, ushort EncodingId, uint Offset);

    /// <summary>
    /// Holds the parsed arrays for a format 4 BMP map.
    /// </summary>
    private sealed class Format4Subtable
    {
        private readonly ReadOnlyMemory<byte> _data;
        private readonly ushort[] _startCodes;
        private readonly ushort[] _endCodes;
        private readonly short[] _idDeltas;
        private readonly ushort[] _idRangeOffsets;
        private readonly int _idRangeOffsetsPosition;
        private readonly ushort _glyphCount;

        /// <summary>
        /// Initializes the parsed format 4 arrays.
        /// </summary>
        /// <param name="data">The bounded subtable data.</param>
        /// <param name="startCodes">The segment start codepoints.</param>
        /// <param name="endCodes">The segment end codepoints.</param>
        /// <param name="idDeltas">The segment glyph deltas.</param>
        /// <param name="idRangeOffsets">The segment glyph-array offsets.</param>
        /// <param name="idRangeOffsetsPosition">The start of the range-offset array.</param>
        /// <param name="glyphCount">The number of glyphs in the font.</param>
        public Format4Subtable(
            ReadOnlyMemory<byte> data,
            ushort[] startCodes,
            ushort[] endCodes,
            short[] idDeltas,
            ushort[] idRangeOffsets,
            int idRangeOffsetsPosition,
            ushort glyphCount
        )
        {
            _data = data;
            _startCodes = startCodes;
            _endCodes = endCodes;
            _idDeltas = idDeltas;
            _idRangeOffsets = idRangeOffsets;
            _idRangeOffsetsPosition = idRangeOffsetsPosition;
            _glyphCount = glyphCount;
        }

        /// <summary>
        /// Resolves a BMP codepoint using the format 4 segments.
        /// </summary>
        /// <param name="codepoint">The BMP codepoint to resolve.</param>
        /// <returns>The glyph index, or zero when the codepoint is absent.</returns>
        public ushort GetGlyphIndex(ushort codepoint)
        {
            var low = 0;
            var high = _endCodes.Length - 1;
            while (low <= high)
            {
                var middle = low + (high - low) / 2;
                if (codepoint > _endCodes[middle])
                    low = middle + 1;
                else if (codepoint < _startCodes[middle])
                    high = middle - 1;
                else
                {
                    var glyphIndex =
                        _idRangeOffsets[middle] == 0
                            ? unchecked((ushort)(codepoint + _idDeltas[middle]))
                            : GetGlyphArrayIndex(middle, codepoint);
                    if (glyphIndex >= _glyphCount && glyphIndex != 0)
                        throw new InvalidDataException(
                            "The 'cmap' table references an invalid glyph index."
                        );
                    return glyphIndex;
                }
            }

            return 0;
        }

        /// <summary>
        /// Resolves a codepoint through the segment's glyph-index array.
        /// </summary>
        /// <param name="segmentIndex">The matching segment index.</param>
        /// <param name="codepoint">The BMP codepoint to resolve.</param>
        /// <returns>The glyph index after applying the segment delta.</returns>
        private ushort GetGlyphArrayIndex(int segmentIndex, ushort codepoint)
        {
            var offset =
                _idRangeOffsetsPosition
                + segmentIndex * sizeof(ushort)
                + _idRangeOffsets[segmentIndex]
                + (codepoint - _startCodes[segmentIndex]) * sizeof(ushort);
            var reader = new TrueTypeReader(_data.Slice(offset, sizeof(ushort)));
            var glyphIndex = reader.ReadUInt16();
            return glyphIndex == 0
                ? (ushort)0
                : unchecked((ushort)(glyphIndex + _idDeltas[segmentIndex]));
        }
    }

    /// <summary>
    /// Holds the sorted character groups for a format 12 map.
    /// </summary>
    private sealed class Format12Subtable
    {
        private readonly Format12Group[] _groups;

        /// <summary>
        /// Initializes the format 12 group array.
        /// </summary>
        /// <param name="groups">The sorted, non-overlapping character groups.</param>
        public Format12Subtable(Format12Group[] groups)
        {
            _groups = groups;
        }

        /// <summary>
        /// Resolves a codepoint using binary search over the groups.
        /// </summary>
        /// <param name="codepoint">The Unicode codepoint to resolve.</param>
        /// <param name="glyphIndex">Receives the mapped glyph index.</param>
        /// <returns><see langword="true"/> when the codepoint belongs to a group.</returns>
        public bool TryGetGlyphIndex(uint codepoint, out ushort glyphIndex)
        {
            var low = 0;
            var high = _groups.Length - 1;
            while (low <= high)
            {
                var middle = low + (high - low) / 2;
                var group = _groups[middle];
                if (codepoint < group.StartCodepoint)
                    high = middle - 1;
                else if (codepoint > group.EndCodepoint)
                    low = middle + 1;
                else
                {
                    glyphIndex = (ushort)(group.StartGlyphIndex + codepoint - group.StartCodepoint);
                    return true;
                }
            }

            glyphIndex = 0;
            return false;
        }
    }

    /// <summary>
    /// Describes a sequential codepoint-to-glyph range in a format 12 map.
    /// </summary>
    /// <param name="StartCodepoint">The first codepoint in the group.</param>
    /// <param name="EndCodepoint">The last codepoint in the group.</param>
    /// <param name="StartGlyphIndex">The glyph index corresponding to the first codepoint.</param>
    private sealed record Format12Group(
        uint StartCodepoint,
        uint EndCodepoint,
        uint StartGlyphIndex
    );
}
