namespace Nexus.Assets.Typography.FontReader.TrueType.Tables.Gpos;

/// <summary>
/// Parses supported GPOS `kern` features and pair-adjustment lookups.
/// </summary>
public sealed class GposTable
{
    private readonly IReadOnlyList<GlyphKerningPair> _pairs;

    /// <summary>
    /// Initializes the parsed GPOS kerning data and source-selection state.
    /// </summary>
    /// <param name="pairs">The selected glyph-index adjustments.</param>
    /// <param name="hasSupportedKerning">Whether supported xAdvance pair data was selected.</param>
    private GposTable(GlyphKerningPair[] pairs, bool hasSupportedKerning)
    {
        _pairs = Array.AsReadOnly(pairs);
        HasSupportedKerning = hasSupportedKerning;
    }

    /// <summary>
    /// Gets the parsed pair adjustments for the selected script and language system.
    /// </summary>
    public IReadOnlyList<GlyphKerningPair> Pairs => _pairs;

    /// <summary>
    /// Gets whether a selected `kern` PairPos lookup has a first-glyph xAdvance representable as scalar kerning.
    /// </summary>
    /// <remarks>Other ValueRecord fields and the second glyph's xAdvance are consumed but not represented.</remarks>
    public bool HasSupportedKerning { get; }

    /// <summary>
    /// Parses supported PairPos format 1 and format 2 data referenced by the selected `kern` feature.
    /// </summary>
    /// <param name="reader">A reader bounded to the complete GPOS table.</param>
    /// <param name="glyphCount">The number of glyphs declared by the font.</param>
    /// <param name="glyphIndices">Glyph indices needed by the current build.</param>
    /// <param name="scriptTag">The four-character script tag, defaulting to Latin.</param>
    /// <param name="languageTag">An optional four-character language-system tag.</param>
    /// <returns>The supported kerning pairs and whether a supported GPOS source was selected.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException">A script or language tag is not four characters.</exception>
    /// <exception cref="InvalidDataException">The GPOS table is malformed or uses unsupported selected PairPos data.</exception>
    public static GposTable Parse(
        TrueTypeReader reader,
        ushort glyphCount,
        IReadOnlyCollection<ushort> glyphIndices,
        string scriptTag = "latn",
        string? languageTag = null
    )
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(glyphIndices);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptTag);
        if (scriptTag.Length != 4)
            throw new ArgumentException(
                "A script tag must contain four characters.",
                nameof(scriptTag)
            );
        if (languageTag is not null && languageTag.Length != 4)
            throw new ArgumentException(
                "A language tag must contain four characters.",
                nameof(languageTag)
            );
        if (reader.Length < 10)
            throw new InvalidDataException("The 'GPOS' table is too short.");

        var majorVersion = reader.ReadUInt16();
        var minorVersion = reader.ReadUInt16();
        if (majorVersion != 1 || minorVersion > 1)
            throw new InvalidDataException("The 'GPOS' table has an unsupported version.");

        var scriptListOffset = reader.ReadUInt16();
        var featureListOffset = reader.ReadUInt16();
        var lookupListOffset = reader.ReadUInt16();
        if (minorVersion == 1)
        {
            if (reader.Length < 14)
                throw new InvalidDataException("The version 1.1 'GPOS' header is truncated.");
            var featureVariationsOffset = reader.ReadUInt32();
            if (featureVariationsOffset != 0)
                throw new InvalidDataException(
                    "GPOS FeatureVariations are not supported by NAP Typography v1."
                );
        }

        var scriptListReader = SliceAtOffset(reader, scriptListOffset, "ScriptList");
        var featureListReader = SliceAtOffset(reader, featureListOffset, "FeatureList");
        var lookupListReader = SliceAtOffset(reader, lookupListOffset, "LookupList");
        var featureIndices = ScriptList.ParseFeatureIndices(
            scriptListReader,
            scriptTag,
            languageTag
        );
        var features = FeatureList.Parse(featureListReader);
        if (featureIndices.Any(featureIndex => featureIndex >= features.Count))
            throw new InvalidDataException("A GPOS LangSys references a missing feature.");
        var lookupIndices = GetKerningLookupIndices(features, featureIndices);
        if (lookupIndices.Count == 0)
            return new GposTable([], hasSupportedKerning: false);

        var candidates = glyphIndices.Where(index => index < glyphCount).ToHashSet();
        var adjustments = new Dictionary<(ushort Left, ushort Right), int>();
        var hasSupportedKerning = LookupList.ParseAndApply(
            lookupListReader,
            lookupIndices,
            glyphCount,
            candidates,
            adjustments
        );
        var pairs = adjustments
            .Where(pair => pair.Value != 0)
            .OrderBy(pair => pair.Key.Left)
            .ThenBy(pair => pair.Key.Right)
            .Select(pair => new GlyphKerningPair(pair.Key.Left, pair.Key.Right, pair.Value))
            .ToArray();
        return new GposTable(pairs, hasSupportedKerning);
    }

    /// <summary>
    /// Finds distinct lookup indices from selected features tagged `kern`.
    /// </summary>
    /// <param name="features">The parsed feature records.</param>
    /// <param name="featureIndices">The indices enabled by the selected language system.</param>
    /// <returns>The referenced lookup indices in feature-list order.</returns>
    private static IReadOnlyList<ushort> GetKerningLookupIndices(
        IReadOnlyList<FeatureRecord> features,
        IReadOnlyCollection<ushort> featureIndices
    )
    {
        var selectedFeatures = featureIndices.ToHashSet();
        var lookupIndices = new List<ushort>();
        var seenLookups = new HashSet<ushort>();
        for (var featureIndex = 0; featureIndex < features.Count; featureIndex++)
        {
            var feature = features[featureIndex];
            if (feature.Tag != "kern" || !selectedFeatures.Contains(checked((ushort)featureIndex)))
                continue;

            foreach (var lookupIndex in feature.LookupIndices)
            {
                if (seenLookups.Add(lookupIndex))
                    lookupIndices.Add(lookupIndex);
            }
        }

        return lookupIndices;
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

    /// <summary>
    /// Parses ScriptList records and selects the feature indices for one language system.
    /// </summary>
    private static class ScriptList
    {
        /// <summary>
        /// Parses script records and resolves the requested script with DFLT fallback.
        /// </summary>
        /// <param name="reader">A reader bounded to ScriptList.</param>
        /// <param name="scriptTag">The requested four-character script tag.</param>
        /// <param name="languageTag">An optional requested language-system tag.</param>
        /// <returns>The feature indices enabled by the selected language system.</returns>
        /// <exception cref="InvalidDataException">The ScriptList or selected language system is malformed.</exception>
        public static IReadOnlyList<ushort> ParseFeatureIndices(
            TrueTypeReader reader,
            string scriptTag,
            string? languageTag
        )
        {
            if (reader.Length < 2)
                throw new InvalidDataException("The GPOS ScriptList is truncated.");

            var scriptCount = reader.ReadUInt16();
            if (scriptCount > (reader.Length - reader.Position) / 6)
                throw new InvalidDataException("The GPOS ScriptList records are truncated.");

            var scripts = new List<(string Tag, ushort Offset)>(scriptCount);
            for (var index = 0; index < scriptCount; index++)
                scripts.Add((reader.ReadTag(), reader.ReadUInt16()));

            var selectedScript = scripts.FirstOrDefault(script => script.Tag == scriptTag);
            if (selectedScript.Tag is null && scriptTag != "DFLT")
                selectedScript = scripts.FirstOrDefault(script => script.Tag == "DFLT");
            if (selectedScript.Tag is null)
                return [];

            var scriptReader = SliceAtOffset(reader, selectedScript.Offset, "Script");
            if (scriptReader.Length < 4)
                throw new InvalidDataException("A GPOS Script table is truncated.");

            var defaultLanguageOffset = scriptReader.ReadUInt16();
            var languageCount = scriptReader.ReadUInt16();
            if (languageCount > (scriptReader.Length - scriptReader.Position) / 6)
                throw new InvalidDataException("The GPOS language-system records are truncated.");

            var languageRecords = new List<(string Tag, ushort Offset)>(languageCount);
            for (var index = 0; index < languageCount; index++)
                languageRecords.Add((scriptReader.ReadTag(), scriptReader.ReadUInt16()));

            ushort languageSystemOffset;
            if (languageTag is not null)
            {
                var requestedLanguage = languageRecords.FirstOrDefault(language =>
                    language.Tag == languageTag
                );
                languageSystemOffset = requestedLanguage.Tag is null
                    ? defaultLanguageOffset
                    : requestedLanguage.Offset;
            }
            else
            {
                languageSystemOffset = defaultLanguageOffset;
                if (languageSystemOffset == 0 && languageRecords.Count > 0)
                    languageSystemOffset = languageRecords[0].Offset;
            }

            if (languageSystemOffset == 0)
                return [];

            var languageSystemReader = SliceAtOffset(scriptReader, languageSystemOffset, "LangSys");
            if (languageSystemReader.Length < 6)
                throw new InvalidDataException("A GPOS LangSys table is truncated.");

            _ = languageSystemReader.ReadUInt16();
            var requiredFeatureIndex = languageSystemReader.ReadUInt16();
            var featureCount = languageSystemReader.ReadUInt16();
            if (featureCount > (languageSystemReader.Length - languageSystemReader.Position) / 2)
                throw new InvalidDataException("The GPOS LangSys feature indices are truncated.");

            var featureIndices = new List<ushort>(featureCount + 1);
            if (requiredFeatureIndex != ushort.MaxValue)
                featureIndices.Add(requiredFeatureIndex);
            for (var index = 0; index < featureCount; index++)
                featureIndices.Add(languageSystemReader.ReadUInt16());
            return featureIndices.Distinct().ToArray();
        }
    }

    /// <summary>
    /// Parses feature records and their lookup index arrays.
    /// </summary>
    private static class FeatureList
    {
        /// <summary>
        /// Parses every feature record in the FeatureList.
        /// </summary>
        /// <param name="reader">A reader bounded to FeatureList.</param>
        /// <returns>The feature records in their declared order.</returns>
        /// <exception cref="InvalidDataException">The FeatureList or one of its records is malformed.</exception>
        public static IReadOnlyList<FeatureRecord> Parse(TrueTypeReader reader)
        {
            if (reader.Length < 2)
                throw new InvalidDataException("The GPOS FeatureList is truncated.");

            var featureCount = reader.ReadUInt16();
            if (featureCount > (reader.Length - reader.Position) / 6)
                throw new InvalidDataException("The GPOS FeatureList records are truncated.");

            var features = new List<FeatureRecord>(featureCount);
            var featureRecords = new List<(string Tag, ushort Offset)>(featureCount);
            for (var index = 0; index < featureCount; index++)
                featureRecords.Add((reader.ReadTag(), reader.ReadUInt16()));

            foreach (var featureRecord in featureRecords)
            {
                var featureReader = SliceAtOffset(reader, featureRecord.Offset, "Feature");
                if (featureReader.Length < 4)
                    throw new InvalidDataException("A GPOS Feature table is truncated.");

                _ = featureReader.ReadUInt16();
                var lookupCount = featureReader.ReadUInt16();
                if (lookupCount > (featureReader.Length - featureReader.Position) / 2)
                    throw new InvalidDataException("A GPOS Feature lookup array is truncated.");

                var lookupIndices = new ushort[lookupCount];
                for (var index = 0; index < lookupCount; index++)
                    lookupIndices[index] = featureReader.ReadUInt16();
                features.Add(new FeatureRecord(featureRecord.Tag, lookupIndices));
            }

            return features;
        }
    }

    /// <summary>
    /// Resolves selected lookup records and applies supported pair-positioning subtables.
    /// </summary>
    private static class LookupList
    {
        /// <summary>
        /// Parses selected lookup records and applies their PairPos subtables.
        /// </summary>
        /// <param name="reader">A reader bounded to LookupList.</param>
        /// <param name="lookupIndices">The lookup indices referenced by selected `kern` features.</param>
        /// <param name="glyphCount">The number of glyphs declared by the font.</param>
        /// <param name="candidates">The glyph indices needed by the current build.</param>
        /// <param name="adjustments">The shared pair adjustments under construction.</param>
        /// <returns>Whether at least one supported PairPos subtable was selected.</returns>
        /// <exception cref="InvalidDataException">A lookup index or selected lookup is malformed or unsupported.</exception>
        public static bool ParseAndApply(
            TrueTypeReader reader,
            IReadOnlyList<ushort> lookupIndices,
            ushort glyphCount,
            IReadOnlySet<ushort> candidates,
            Dictionary<(ushort Left, ushort Right), int> adjustments
        )
        {
            if (reader.Length < 2)
                throw new InvalidDataException("The GPOS LookupList is truncated.");

            var lookupCount = reader.ReadUInt16();
            if (lookupCount > (reader.Length - reader.Position) / 2)
                throw new InvalidDataException("The GPOS LookupList offsets are truncated.");

            var lookupOffsets = new ushort[lookupCount];
            for (var index = 0; index < lookupCount; index++)
                lookupOffsets[index] = reader.ReadUInt16();

            var hasSupportedKerning = false;
            foreach (var lookupIndex in lookupIndices)
            {
                if (lookupIndex >= lookupOffsets.Length)
                    throw new InvalidDataException("A GPOS feature references a missing lookup.");

                var lookupReader = SliceAtOffset(reader, lookupOffsets[lookupIndex], "Lookup");
                if (lookupReader.Length < 6)
                    throw new InvalidDataException("A GPOS Lookup table is truncated.");

                var lookupType = lookupReader.ReadUInt16();
                var lookupFlags = lookupReader.ReadUInt16();
                var subtableCount = lookupReader.ReadUInt16();
                if (subtableCount > (lookupReader.Length - lookupReader.Position) / 2)
                    throw new InvalidDataException("A GPOS Lookup subtable array is truncated.");

                var subtableOffsets = new ushort[subtableCount];
                for (var index = 0; index < subtableCount; index++)
                    subtableOffsets[index] = lookupReader.ReadUInt16();

                if (lookupType != 2)
                    continue;
                if (lookupFlags != 0)
                    throw new InvalidDataException(
                        "GPOS pair-positioning lookup flags are not supported by NAP Typography v1."
                    );

                foreach (var subtableOffset in subtableOffsets)
                {
                    var positioningReader = SliceAtOffset(lookupReader, subtableOffset, "PairPos");
                    if (
                        PairPositioning.ParseAndApply(
                            positioningReader,
                            glyphCount,
                            candidates,
                            adjustments
                        )
                    )
                        hasSupportedKerning = true;
                }
            }

            return hasSupportedKerning;
        }
    }

    /// <summary>
    /// Describes one GPOS feature and the lookups it enables.
    /// </summary>
    private sealed record FeatureRecord(string Tag, ushort[] LookupIndices);
}
