using Nexus.AssetPipeline.Typography.FontReader.TrueType;

namespace Nexus.AssetPipeline.Typography.FontReader.TrueType.Tables.Gpos;

/// <summary>
/// Parses explicit and class-based GPOS Pair Adjustment subtables.
/// </summary>
internal static class PairPositioning
{
    /// <summary>
    /// Parses one PairPos subtable and adds its font-unit advance adjustments.
    /// </summary>
    /// <param name="reader">A reader bounded to the PairPos subtable.</param>
    /// <param name="glyphCount">The number of glyphs declared by the font.</param>
    /// <param name="candidates">The glyph indices needed by the current build.</param>
    /// <param name="adjustments">The shared pair adjustments under construction.</param>
    /// <returns><see langword="true"/> when the subtable has a supported PairPos format.</returns>
    /// <exception cref="InvalidDataException">The PairPos subtable is malformed or uses an unsupported format.</exception>
    public static bool ParseAndApply(
        TrueTypeReader reader,
        ushort glyphCount,
        IReadOnlySet<ushort> candidates,
        Dictionary<(ushort Left, ushort Right), int> adjustments
    )
    {
        if (reader.Length < 10)
            throw new InvalidDataException("A GPOS PairPos subtable is truncated.");

        var format = reader.ReadUInt16();
        var coverageOffset = reader.ReadUInt16();
        var valueFormat1 = reader.ReadUInt16();
        var valueFormat2 = reader.ReadUInt16();
        ValidateValueFormat(valueFormat1);
        ValidateValueFormat(valueFormat2);
        var coverage = ParseCoverage(SliceAtOffset(reader, coverageOffset, "Coverage"), glyphCount);

        return format switch
        {
            1 => ParseFormatOne(
                reader,
                coverage,
                valueFormat1,
                valueFormat2,
                glyphCount,
                candidates,
                adjustments
            ),
            2 => ParseFormatTwo(
                reader,
                coverage,
                valueFormat1,
                valueFormat2,
                glyphCount,
                candidates,
                adjustments
            ),
            _ => throw new InvalidDataException(
                $"The GPOS PairPos format {format} is not supported by NAP Typography v1."
            ),
        };
    }

    /// <summary>
    /// Parses PairPos format 1's explicit pair sets.
    /// </summary>
    /// <param name="reader">The reader positioned after the common PairPos header.</param>
    /// <param name="coverage">The covered first glyphs in coverage order.</param>
    /// <param name="valueFormat1">The first glyph ValueRecord format.</param>
    /// <param name="valueFormat2">The second glyph ValueRecord format.</param>
    /// <param name="glyphCount">The number of glyphs declared by the font.</param>
    /// <param name="candidates">The glyph indices needed by the current build.</param>
    /// <param name="adjustments">The shared pair adjustments under construction.</param>
    /// <returns><see langword="true"/> when the subtable contains xAdvance fields.</returns>
    /// <exception cref="InvalidDataException">The pair-set offsets or records are malformed.</exception>
    private static bool ParseFormatOne(
        TrueTypeReader reader,
        IReadOnlyList<ushort> coverage,
        ushort valueFormat1,
        ushort valueFormat2,
        ushort glyphCount,
        IReadOnlySet<ushort> candidates,
        Dictionary<(ushort Left, ushort Right), int> adjustments
    )
    {
        var pairSetCount = reader.ReadUInt16();
        if (pairSetCount != coverage.Count || pairSetCount > (reader.Length - reader.Position) / 2)
            throw new InvalidDataException("A GPOS PairPos format 1 pair-set count is invalid.");

        var pairSetOffsets = new ushort[pairSetCount];
        for (var index = 0; index < pairSetCount; index++)
            pairSetOffsets[index] = reader.ReadUInt16();

        var recordSize = 2 + GetValueRecordSize(valueFormat1) + GetValueRecordSize(valueFormat2);
        for (var pairSetIndex = 0; pairSetIndex < pairSetCount; pairSetIndex++)
        {
            var pairSetReader = SliceAtOffset(reader, pairSetOffsets[pairSetIndex], "PairSet");
            if (pairSetReader.Length < 2)
                throw new InvalidDataException("A GPOS PairSet table is truncated.");

            var pairValueCount = pairSetReader.ReadUInt16();
            if (pairValueCount > (pairSetReader.Length - pairSetReader.Position) / recordSize)
                throw new InvalidDataException("A GPOS PairSet value array is truncated.");

            var leftGlyphIndex = coverage[pairSetIndex];
            ushort? previousRightGlyphIndex = null;
            for (var pairIndex = 0; pairIndex < pairValueCount; pairIndex++)
            {
                var rightGlyphIndex = pairSetReader.ReadUInt16();
                if (rightGlyphIndex >= glyphCount)
                    throw new InvalidDataException(
                        "A GPOS PairSet contains an invalid glyph index."
                    );
                if (previousRightGlyphIndex is { } previous && rightGlyphIndex <= previous)
                    throw new InvalidDataException("A GPOS PairSet is not strictly sorted.");

                var adjustment =
                    ReadValueRecord(pairSetReader, valueFormat1)
                    + ReadValueRecord(pairSetReader, valueFormat2);
                if (candidates.Contains(leftGlyphIndex) && candidates.Contains(rightGlyphIndex))
                    AddAdjustment(adjustments, leftGlyphIndex, rightGlyphIndex, adjustment);
                previousRightGlyphIndex = rightGlyphIndex;
            }
        }

        return (valueFormat1 & 0x0004) != 0 || (valueFormat2 & 0x0004) != 0;
    }

    /// <summary>
    /// Parses PairPos format 2's class definitions and adjustment matrix.
    /// </summary>
    /// <param name="reader">The reader positioned after the common PairPos header.</param>
    /// <param name="coverage">The glyphs eligible as the first glyph in a pair.</param>
    /// <param name="valueFormat1">The first glyph ValueRecord format.</param>
    /// <param name="valueFormat2">The second glyph ValueRecord format.</param>
    /// <param name="glyphCount">The number of glyphs declared by the font.</param>
    /// <param name="candidates">The glyph indices needed by the current build.</param>
    /// <param name="adjustments">The shared pair adjustments under construction.</param>
    /// <returns><see langword="true"/> when the subtable contains xAdvance fields.</returns>
    /// <exception cref="InvalidDataException">The class definitions or class matrix are malformed.</exception>
    private static bool ParseFormatTwo(
        TrueTypeReader reader,
        IReadOnlyList<ushort> coverage,
        ushort valueFormat1,
        ushort valueFormat2,
        ushort glyphCount,
        IReadOnlySet<ushort> candidates,
        Dictionary<(ushort Left, ushort Right), int> adjustments
    )
    {
        if (reader.Length - reader.Position < 8)
            throw new InvalidDataException("A GPOS PairPos format 2 header is truncated.");

        var classDef1Offset = reader.ReadUInt16();
        var classDef2Offset = reader.ReadUInt16();
        var class1Count = reader.ReadUInt16();
        var class2Count = reader.ReadUInt16();
        if (class1Count == 0 || class2Count == 0)
            throw new InvalidDataException("A GPOS PairPos format 2 has an empty class matrix.");

        var classDef1 = ParseClassDefinition(
            SliceAtOffset(reader, classDef1Offset, "ClassDef1"),
            glyphCount,
            class1Count
        );
        var classDef2 = ParseClassDefinition(
            SliceAtOffset(reader, classDef2Offset, "ClassDef2"),
            glyphCount,
            class2Count
        );
        var leftGlyphsByClass = GroupCandidates(candidates, classDef1, class1Count, coverage);
        var rightGlyphsByClass = GroupCandidates(candidates, classDef2, class2Count, null);
        var recordSize = GetValueRecordSize(valueFormat1) + GetValueRecordSize(valueFormat2);
        var matrixCount = (long)class1Count * class2Count;
        if (recordSize != 0 && matrixCount > (reader.Length - reader.Position) / recordSize)
            throw new InvalidDataException("A GPOS PairPos class matrix is truncated.");

        if (recordSize == 0)
            return false;

        for (var class1 = 0; class1 < class1Count; class1++)
        {
            for (var class2 = 0; class2 < class2Count; class2++)
            {
                var adjustment =
                    ReadValueRecord(reader, valueFormat1) + ReadValueRecord(reader, valueFormat2);
                if (adjustment == 0)
                    continue;

                foreach (var leftGlyphIndex in leftGlyphsByClass[class1])
                {
                    foreach (var rightGlyphIndex in rightGlyphsByClass[class2])
                        AddAdjustment(adjustments, leftGlyphIndex, rightGlyphIndex, adjustment);
                }
            }
        }

        return (valueFormat1 & 0x0004) != 0 || (valueFormat2 & 0x0004) != 0;
    }

    /// <summary>
    /// Parses a format-one or format-two class definition into glyph-indexed class values.
    /// </summary>
    /// <param name="reader">A reader bounded to the ClassDef table.</param>
    /// <param name="glyphCount">The number of glyphs declared by the font.</param>
    /// <param name="classCount">The class count declared by its PairPos matrix.</param>
    /// <returns>The class number for every glyph, with unspecified glyphs in class zero.</returns>
    /// <exception cref="InvalidDataException">The ClassDef format or ranges are invalid.</exception>
    private static ushort[] ParseClassDefinition(
        TrueTypeReader reader,
        ushort glyphCount,
        ushort classCount
    )
    {
        if (reader.Length < 4)
            throw new InvalidDataException("A GPOS ClassDef table is truncated.");

        var classes = new ushort[glyphCount];
        var format = reader.ReadUInt16();
        if (format == 1)
        {
            var startGlyphIndex = reader.ReadUInt16();
            var glyphRangeCount = reader.ReadUInt16();
            if (
                startGlyphIndex > glyphCount
                || glyphRangeCount > glyphCount - startGlyphIndex
                || glyphRangeCount > (reader.Length - reader.Position) / 2
            )
                throw new InvalidDataException("A GPOS ClassDef format 1 range is invalid.");

            for (var index = 0; index < glyphRangeCount; index++)
            {
                var classValue = reader.ReadUInt16();
                if (classValue >= classCount)
                    throw new InvalidDataException("A GPOS ClassDef references an invalid class.");
                classes[startGlyphIndex + index] = classValue;
            }

            return classes;
        }

        if (format != 2)
            throw new InvalidDataException($"The GPOS ClassDef format {format} is not supported.");

        var rangeCount = reader.ReadUInt16();
        if (rangeCount > (reader.Length - reader.Position) / 6)
            throw new InvalidDataException("A GPOS ClassDef format 2 range array is truncated.");

        ushort previousEndGlyphIndex = 0;
        for (var rangeIndex = 0; rangeIndex < rangeCount; rangeIndex++)
        {
            var startGlyphIndex = reader.ReadUInt16();
            var endGlyphIndex = reader.ReadUInt16();
            var classValue = reader.ReadUInt16();
            if (
                startGlyphIndex > endGlyphIndex
                || endGlyphIndex >= glyphCount
                || (rangeIndex > 0 && startGlyphIndex <= previousEndGlyphIndex)
                || classValue >= classCount
            )
                throw new InvalidDataException("A GPOS ClassDef format 2 range is invalid.");

            for (var glyphIndex = startGlyphIndex; glyphIndex <= endGlyphIndex; glyphIndex++)
                classes[glyphIndex] = classValue;
            previousEndGlyphIndex = endGlyphIndex;
        }

        return classes;
    }

    /// <summary>
    /// Groups candidate glyphs by class, optionally requiring first-glyph coverage.
    /// </summary>
    /// <param name="candidates">The glyph indices needed by the current build.</param>
    /// <param name="classValues">The class value for each glyph index.</param>
    /// <param name="classCount">The number of classes in the PairPos matrix.</param>
    /// <param name="coverage">Optional eligible first-glyph coverage.</param>
    /// <returns>Candidate glyph groups indexed by class number.</returns>
    /// <exception cref="InvalidDataException">A candidate resolves to a class outside the matrix.</exception>
    private static List<ushort>[] GroupCandidates(
        IReadOnlySet<ushort> candidates,
        IReadOnlyList<ushort> classValues,
        ushort classCount,
        IReadOnlyList<ushort>? coverage
    )
    {
        var groups = new List<ushort>[classCount];
        for (var classIndex = 0; classIndex < classCount; classIndex++)
            groups[classIndex] = [];

        var coveredGlyphs = coverage?.ToHashSet();
        foreach (var glyphIndex in candidates.Order())
        {
            if (coveredGlyphs is not null && !coveredGlyphs.Contains(glyphIndex))
                continue;

            var classValue = classValues[glyphIndex];
            if (classValue >= classCount)
                throw new InvalidDataException("A GPOS ClassDef references an invalid class.");
            groups[classValue].Add(glyphIndex);
        }

        return groups;
    }

    /// <summary>
    /// Parses a Coverage table in format 1 or format 2.
    /// </summary>
    /// <param name="reader">A reader bounded to the Coverage table.</param>
    /// <param name="glyphCount">The number of glyphs declared by the font.</param>
    /// <returns>Covered glyph indices in coverage order.</returns>
    /// <exception cref="InvalidDataException">The Coverage table is malformed or uses an unsupported format.</exception>
    private static ushort[] ParseCoverage(TrueTypeReader reader, ushort glyphCount)
    {
        if (reader.Length < 4)
            throw new InvalidDataException("A GPOS Coverage table is truncated.");

        var format = reader.ReadUInt16();
        if (format == 1)
        {
            var glyphCountInCoverage = reader.ReadUInt16();
            if (glyphCountInCoverage > (reader.Length - reader.Position) / 2)
                throw new InvalidDataException("A GPOS Coverage format 1 array is truncated.");

            var glyphs = new ushort[glyphCountInCoverage];
            ushort? previousGlyphIndex = null;
            for (var index = 0; index < glyphs.Length; index++)
            {
                var glyphIndex = reader.ReadUInt16();
                if (
                    glyphIndex >= glyphCount
                    || (previousGlyphIndex is { } previous && glyphIndex <= previous)
                )
                    throw new InvalidDataException("A GPOS Coverage glyph array is invalid.");
                glyphs[index] = glyphIndex;
                previousGlyphIndex = glyphIndex;
            }

            return glyphs;
        }

        if (format != 2)
            throw new InvalidDataException($"The GPOS Coverage format {format} is not supported.");

        var rangeCount = reader.ReadUInt16();
        if (rangeCount > (reader.Length - reader.Position) / 6)
            throw new InvalidDataException("A GPOS Coverage format 2 range array is truncated.");

        var covered = new List<ushort>();
        ushort previousEndGlyphIndex = 0;
        for (var rangeIndex = 0; rangeIndex < rangeCount; rangeIndex++)
        {
            var startGlyphIndex = reader.ReadUInt16();
            var endGlyphIndex = reader.ReadUInt16();
            var startCoverageIndex = reader.ReadUInt16();
            if (
                startGlyphIndex > endGlyphIndex
                || endGlyphIndex >= glyphCount
                || (rangeIndex > 0 && startGlyphIndex <= previousEndGlyphIndex)
                || startCoverageIndex != covered.Count
            )
                throw new InvalidDataException("A GPOS Coverage format 2 range is invalid.");

            for (var glyphIndex = startGlyphIndex; glyphIndex <= endGlyphIndex; glyphIndex++)
                covered.Add(glyphIndex);
            previousEndGlyphIndex = endGlyphIndex;
        }

        return covered.ToArray();
    }

    /// <summary>
    /// Reads one ValueRecord and returns its xAdvance component in font units.
    /// </summary>
    /// <param name="reader">The reader positioned at the ValueRecord.</param>
    /// <param name="valueFormat">The fields present in the ValueRecord.</param>
    /// <returns>The signed xAdvance value, or zero when absent.</returns>
    /// <exception cref="InvalidDataException">A selected ValueRecord is truncated.</exception>
    private static int ReadValueRecord(TrueTypeReader reader, ushort valueFormat)
    {
        var xAdvance = 0;
        for (var bit = 1; bit <= 0x80; bit <<= 1)
        {
            if ((valueFormat & bit) == 0)
                continue;

            var value = reader.ReadUInt16();
            if (bit == 0x0004)
                xAdvance = unchecked((short)value);
        }

        return xAdvance;
    }

    /// <summary>
    /// Gets the byte width of one ValueRecord from its field mask.
    /// </summary>
    /// <param name="valueFormat">The ValueRecord field mask.</param>
    /// <returns>The number of 16-bit fields present.</returns>
    private static int GetValueRecordSize(ushort valueFormat)
    {
        var fieldCount = 0;
        for (var bit = 1; bit <= 0x80; bit <<= 1)
        {
            if ((valueFormat & bit) != 0)
                fieldCount++;
        }

        return fieldCount * 2;
    }

    /// <summary>
    /// Rejects reserved ValueRecord format bits.
    /// </summary>
    /// <param name="valueFormat">The field mask to validate.</param>
    /// <exception cref="InvalidDataException">The mask contains reserved bits.</exception>
    private static void ValidateValueFormat(ushort valueFormat)
    {
        if ((valueFormat & 0xFF00) != 0)
            throw new InvalidDataException("A GPOS ValueFormat contains reserved bits.");
    }

    /// <summary>
    /// Adds a nonzero adjustment to the shared glyph-pair map.
    /// </summary>
    /// <param name="adjustments">The map being constructed.</param>
    /// <param name="leftGlyphIndex">The first glyph index.</param>
    /// <param name="rightGlyphIndex">The second glyph index.</param>
    /// <param name="adjustment">The adjustment in font units.</param>
    private static void AddAdjustment(
        Dictionary<(ushort Left, ushort Right), int> adjustments,
        ushort leftGlyphIndex,
        ushort rightGlyphIndex,
        int adjustment
    )
    {
        if (adjustment == 0)
            return;

        var key = (leftGlyphIndex, rightGlyphIndex);
        adjustments[key] = checked(adjustments.GetValueOrDefault(key) + adjustment);
    }

    /// <summary>
    /// Creates a reader for a table-relative nonzero offset.
    /// </summary>
    /// <param name="reader">The containing table reader.</param>
    /// <param name="offset">The offset from the beginning of the containing table.</param>
    /// <param name="name">The name used in malformed-data diagnostics.</param>
    /// <returns>A reader bounded to the data following the offset.</returns>
    /// <exception cref="InvalidDataException">The offset is zero or outside the containing table.</exception>
    private static TrueTypeReader SliceAtOffset(TrueTypeReader reader, ushort offset, string name)
    {
        if (offset == 0 || offset >= reader.Length)
            throw new InvalidDataException($"The GPOS {name} offset is invalid.");
        return new TrueTypeReader(reader.Slice(offset, reader.Length - offset));
    }
}
