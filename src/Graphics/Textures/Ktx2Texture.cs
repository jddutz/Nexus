namespace Nexus.Graphics.Textures;

/// <summary>Reads and writes the NAP KTX2 subset: 2D, uncompressed RGBA8 sRGB.</summary>
public static class Ktx2Texture
{
    private static ReadOnlySpan<byte> Identifier => [0xab, 0x4b, 0x54, 0x58, 0x20, 0x32, 0x30, 0xbb, 13, 10, 26, 10];

    public static void Write(Stream stream, IReadOnlyList<TextureMipLevel> levels)
    {
        ValidateChain(levels);
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
        var dfdOffset = checked(80u + (uint)levels.Count * 24);
        writer.Write(Identifier);
        foreach (var value in new uint[] { 43, 1, levels[0].Width, levels[0].Height, 0, 0, 1, (uint)levels.Count, 0, dfdOffset, 92, 0, 0 }) writer.Write(value);
        writer.Write(0ul); writer.Write(0ul);
        var offsets = new ulong[levels.Count];
        ulong offset = dfdOffset + 92;
        for (var i = levels.Count - 1; i >= 0; i--) { offsets[i] = offset; offset += (ulong)levels[i].Pixels.Length; }
        for (var i = 0; i < levels.Count; i++) { writer.Write(offsets[i]); writer.Write((ulong)levels[i].Pixels.Length); writer.Write((ulong)levels[i].Pixels.Length); }
        // Khronos basic DFD: RGBSDA, BT709, sRGB, straight alpha, four 8-bit samples.
        writer.Write(92u); writer.Write(0u); writer.Write((ushort)2); writer.Write((ushort)88);
        writer.Write(new byte[] { 1, 1, 2, 0, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0 });
        for (var channel = 0; channel < 4; channel++)
        {
            writer.Write((ushort)(channel * 8)); writer.Write((byte)7);
            writer.Write((byte)(channel == 3 ? 15 | 16 : channel)); // Alpha has the linear qualifier.
            writer.Write(0u); writer.Write(0u); writer.Write(255u);
        }
        for (var i = levels.Count - 1; i >= 0; i--) writer.Write(levels[i].Pixels);
    }

    public static TextureMipLevel[] Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true);
        if (!reader.ReadBytes(12).AsSpan().SequenceEqual(Identifier)) throw new InvalidDataException("Invalid KTX2 identifier.");
        var format = reader.ReadUInt32(); var typeSize = reader.ReadUInt32();
        var width = reader.ReadUInt32(); var height = reader.ReadUInt32();
        var depth = reader.ReadUInt32(); var layers = reader.ReadUInt32(); var faces = reader.ReadUInt32();
        var count = reader.ReadUInt32(); var compression = reader.ReadUInt32();
        if (format != 43 || typeSize != 1 || depth != 0 || layers != 0 || faces != 1 || compression != 0)
            throw new NotSupportedException("Only 2D RGBA8 sRGB KTX2 textures without supercompression are supported.");
        if (width == 0 || height == 0 || count == 0 || count > 32 || count > 1 + System.Numerics.BitOperations.Log2(Math.Max(width, height)))
            throw new InvalidDataException("Invalid KTX2 dimensions or mip count.");
        var dfdOffset = reader.ReadUInt32(); var dfdLength = reader.ReadUInt32();
        if (dfdLength < 28 || dfdOffset < 80 + count * 24 || (ulong)dfdOffset + dfdLength > (ulong)stream.Length)
            throw new InvalidDataException("Invalid KTX2 data format descriptor.");
        stream.Position = 80;
        var levels = new TextureMipLevel[count];
        ulong previousStart = (ulong)stream.Length;
        for (var i = 0; i < count; i++)
        {
            var offset = reader.ReadUInt64(); var length = reader.ReadUInt64(); var uncompressed = reader.ReadUInt64();
            var expected = checked((ulong)width * height * 4);
            if (length != expected || uncompressed != expected || length > int.MaxValue || offset % 4 != 0
                || offset < (ulong)dfdOffset + dfdLength || offset > (ulong)stream.Length || length > (ulong)stream.Length - offset
                || offset + length > previousStart)
                throw new InvalidDataException("Invalid KTX2 level index.");
            var position = stream.Position; stream.Position = checked((long)offset);
            var pixels = reader.ReadBytes((int)length);
            if (pixels.Length != (int)length) throw new EndOfStreamException();
            levels[i] = new(width, height, pixels); stream.Position = position; previousStart = offset;
            width = Math.Max(1, width / 2); height = Math.Max(1, height / 2);
        }
        return levels;
    }

    private static void ValidateChain(IReadOnlyList<TextureMipLevel> levels)
    {
        if (levels.Count == 0) throw new ArgumentException("A texture needs a base level.");
        uint width = levels[0].Width, height = levels[0].Height;
        for (var i = 0; i < levels.Count; i++)
        {
            TextureMipmaps.Validate(levels[i]);
            if (levels[i].Width != width || levels[i].Height != height || (i > 0 && levels[i - 1].Width == 1 && levels[i - 1].Height == 1))
                throw new ArgumentException("Invalid mip chain dimensions.");
            width = Math.Max(1, width / 2); height = Math.Max(1, height / 2);
        }
    }
}
