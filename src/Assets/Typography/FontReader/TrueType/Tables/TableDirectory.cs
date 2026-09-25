using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Nexus.Assets.Typography.FontReader.TrueType;

namespace Nexus.Assets.Typography.FontReader.TrueType.Tables;

/// <summary>
/// Contains the table records declared by an SFNT font header.
/// </summary>
public sealed class TableDirectory
{
    private readonly ReadOnlyCollection<TableRecord> _tables;
    private readonly Dictionary<string, TableRecord> _tablesByTag;

    private TableDirectory(TableRecord[] tables)
    {
        _tables = Array.AsReadOnly(tables);
        _tablesByTag = tables.ToDictionary(table => table.Tag, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the table records in the order they appear in the font file.
    /// </summary>
    public IReadOnlyList<TableRecord> Tables => _tables;

    /// <summary>
    /// Parses an SFNT header and its table records at the reader's current position.
    /// </summary>
    /// <param name="reader">The reader positioned at the start of the SFNT header.</param>
    /// <returns>The parsed table directory.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="InvalidDataException">The header or a table record is invalid.</exception>
    public static TableDirectory Parse(TrueTypeReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var scalerType = reader.ReadUInt32();
        if (!IsSupportedScalerType(scalerType))
            throw new InvalidDataException("The font does not have a supported SFNT signature.");

        var tableCount = reader.ReadUInt16();
        _ = reader.ReadUInt16();
        _ = reader.ReadUInt16();
        _ = reader.ReadUInt16();

        if (tableCount == 0)
            throw new InvalidDataException("The SFNT table directory is empty.");

        var tables = new TableRecord[tableCount];
        var seenTags = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < tables.Length; index++)
        {
            var tag = reader.ReadTag();
            var checksum = reader.ReadUInt32();
            var offset = reader.ReadUInt32();
            var length = reader.ReadUInt32();

            if (!seenTags.Add(tag))
                throw new InvalidDataException(
                    $"The SFNT directory contains duplicate table tag '{tag}'."
                );

            if (offset > (uint)reader.Length || length > (uint)reader.Length - offset)
                throw new InvalidDataException(
                    $"The SFNT table '{tag}' extends beyond the font data."
                );

            tables[index] = new TableRecord(tag, checksum, offset, length);
        }

        return new TableDirectory(tables);
    }

    /// <summary>
    /// Looks up a table record by its four-character tag.
    /// </summary>
    /// <param name="tag">The exact table tag to find.</param>
    /// <param name="table">Receives the matching record when found.</param>
    /// <returns><see langword="true"/> if the directory contains the tag.</returns>
    public bool TryGetTable(string tag, [NotNullWhen(true)] out TableRecord? table)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return _tablesByTag.TryGetValue(tag, out table);
    }

    /// <summary>
    /// Determines whether the SFNT scaler type is supported for directory parsing.
    /// </summary>
    /// <param name="scalerType">The big-endian scaler type value.</param>
    /// <returns><see langword="true"/> for a recognized SFNT signature.</returns>
    private static bool IsSupportedScalerType(uint scalerType) =>
        scalerType is 0x00010000 or 0x4F54544F or 0x74727565 or 0x74797031;
}

/// <summary>
/// Describes one table in an SFNT font file.
/// </summary>
public sealed class TableRecord
{
    /// <summary>
    /// Initializes a table record with its directory values.
    /// </summary>
    /// <param name="tag">The four-character table tag.</param>
    /// <param name="checksum">The table checksum from the SFNT directory.</param>
    /// <param name="offset">The table's offset from the start of the font data.</param>
    /// <param name="length">The table length in bytes.</param>
    internal TableRecord(string tag, uint checksum, uint offset, uint length)
    {
        Tag = tag;
        Checksum = checksum;
        Offset = offset;
        Length = length;
    }

    /// <summary>
    /// Gets the four-character table tag.
    /// </summary>
    public string Tag { get; }

    /// <summary>
    /// Gets the table checksum declared in the directory.
    /// </summary>
    public uint Checksum { get; }

    /// <summary>
    /// Gets the table offset from the beginning of the font data.
    /// </summary>
    public uint Offset { get; }

    /// <summary>
    /// Gets the table length in bytes.
    /// </summary>
    public uint Length { get; }
}
