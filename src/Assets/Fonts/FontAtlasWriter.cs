namespace Nexus.Assets.Fonts;

using System.Buffers.Binary;
using System.IO.Compression;

/// <summary>
/// Writes the binary atlas artifact produced by the font builder.
/// </summary>
public static class FontAtlasWriter
{
    private static readonly uint[] CrcTable = CreateCrcTable();

    /// <summary>
    /// Writes the generated atlas to the specified artifact path.
    /// </summary>
    /// <param name="atlasPath">The path of the atlas artifact to write.</param>
    /// <param name="result">The generated font data containing the atlas.</param>
    public static void Write(string atlasPath, FontBuildResult result)
    {
        ArgumentNullException.ThrowIfNull(atlasPath);
        ArgumentNullException.ThrowIfNull(result);
        Validate(result);

        try
        {
            var directory = Path.GetDirectoryName(atlasPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllBytes(atlasPath, result.Atlas.Pixels);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new FontBuildException($"Could not write font atlas '{atlasPath}'.", exception);
        }
    }

    /// <summary>
    /// Writes the generated RGB8 atlas as a PNG diagnostic image without changing its row order.
    /// </summary>
    /// <param name="atlasPath">The path of the PNG image to write.</param>
    /// <param name="result">The generated font data containing the atlas.</param>
    public static void WritePng(string atlasPath, FontBuildResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(atlasPath);
        ArgumentNullException.ThrowIfNull(result);
        Validate(result);

        try
        {
            var directory = Path.GetDirectoryName(atlasPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var scanlineLength = checked(result.Atlas.Width * 3 + 1);
            var scanlines = new byte[checked(scanlineLength * result.Atlas.Height)];
            for (var row = 0; row < result.Atlas.Height; row++)
            {
                var sourceOffset = checked(row * result.Atlas.Width * 3);
                var destinationOffset = checked(row * scanlineLength + 1);
                Buffer.BlockCopy(
                    result.Atlas.Pixels,
                    sourceOffset,
                    scanlines,
                    destinationOffset,
                    result.Atlas.Width * 3
                );
            }

            using var compressed = new MemoryStream();
            using (var compressor = new ZLibStream(compressed, CompressionLevel.Optimal, true))
                compressor.Write(scanlines);

            using var png = File.Create(atlasPath);
            png.Write([137, 80, 78, 71, 13, 10, 26, 10]);

            Span<byte> header = stackalloc byte[13];
            BinaryPrimitives.WriteInt32BigEndian(header, result.Atlas.Width);
            BinaryPrimitives.WriteInt32BigEndian(header[4..], result.Atlas.Height);
            header[8] = 8;
            header[9] = 2;
            WriteChunk(png, "IHDR"u8, header);
            WriteChunk(
                png,
                "IDAT"u8,
                compressed.GetBuffer().AsSpan(0, checked((int)compressed.Length))
            );
            WriteChunk(png, "IEND"u8, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new FontBuildException(
                $"Could not write font atlas PNG '{atlasPath}'.",
                exception
            );
        }
    }

    /// <summary>
    /// Validates the atlas dimensions, pixel count, and glyph payload.
    /// </summary>
    /// <param name="result">The generated font data to validate.</param>
    private static void Validate(FontBuildResult result)
    {
        if (result.Atlas.Width <= 0 || result.Atlas.Height <= 0)
            throw new FontBuildException("Font builder returned invalid atlas dimensions.");

        var expectedLength = checked(result.Atlas.Width * result.Atlas.Height * 3);
        if (result.Atlas.Pixels.Length != expectedLength)
            throw new FontBuildException(
                $"Font builder returned {result.Atlas.Pixels.Length} atlas bytes; expected {expectedLength}."
            );
        if (result.Glyphs.Count == 0)
            throw new FontBuildException("Font builder returned no glyphs.");
    }

    /// <summary>
    /// Writes one PNG chunk with its length, type, data, and CRC-32 checksum.
    /// </summary>
    /// <param name="stream">The destination PNG stream.</param>
    /// <param name="type">The four-byte chunk type.</param>
    /// <param name="data">The chunk payload.</param>
    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> value = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(value, data.Length);
        stream.Write(value);
        stream.Write(type);
        stream.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(value, ComputeCrc32(type, data));
        stream.Write(value);
    }

    /// <summary>
    /// Computes the PNG CRC-32 checksum for a chunk type and its payload.
    /// </summary>
    /// <param name="type">The four-byte chunk type.</param>
    /// <param name="data">The chunk payload.</param>
    /// <returns>The checksum.</returns>
    private static uint ComputeCrc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var value in type)
            crc = CrcTable[(crc ^ value) & 0xff] ^ (crc >> 8);
        foreach (var value in data)
            crc = CrcTable[(crc ^ value) & 0xff] ^ (crc >> 8);
        return ~crc;
    }

    /// <summary>
    /// Creates the lookup table used by the PNG CRC-32 calculation.
    /// </summary>
    /// <returns>The CRC-32 lookup table.</returns>
    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
                value = (value & 1) == 0 ? value >> 1 : 0xedb88320 ^ (value >> 1);
            table[index] = value;
        }
        return table;
    }
}
