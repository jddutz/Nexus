using Nexus.AssetPipeline.Typography.FontReader.TrueType.Tables;

namespace Nexus.AssetPipeline.Typography.FontReader.TrueType;

/// <summary>
/// Opens an SFNT font and exposes its validated table directory and table data.
/// </summary>
public sealed class TrueTypeFontReader
{
    private readonly ReadOnlyMemory<byte> _data;

    /// <summary>
    /// Initializes a font reader from in-memory font data.
    /// </summary>
    /// <param name="data">The complete font file contents.</param>
    public TrueTypeFontReader(ReadOnlyMemory<byte> data)
    {
        _data = data;
        TableDirectory = TableDirectory.Parse(new TrueTypeReader(data));
    }

    /// <summary>
    /// Gets the parsed SFNT table directory.
    /// </summary>
    public TableDirectory TableDirectory { get; }

    /// <summary>
    /// Opens a font file from disk.
    /// </summary>
    /// <param name="path">The path to a TrueType or OpenType font file.</param>
    /// <returns>A reader for the font.</returns>
    public static TrueTypeFontReader Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new TrueTypeFontReader(File.ReadAllBytes(path));
    }

    /// <summary>
    /// Gets the bytes for a table declared in the font directory.
    /// </summary>
    /// <param name="tag">The exact four-character table tag.</param>
    /// <returns>A bounded memory view of the table.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tag"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">The directory does not contain the tag.</exception>
    public ReadOnlyMemory<byte> GetTable(string tag)
    {
        if (!TableDirectory.TryGetTable(tag, out var table))
            throw new KeyNotFoundException($"The font does not contain the '{tag}' table.");

        return _data.Slice(checked((int)table.Offset), checked((int)table.Length));
    }
}