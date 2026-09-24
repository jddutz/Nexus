using System.Buffers.Binary;
using System.Text;

namespace Nexus.AssetPipeline.Typography.FontReader.TrueType;

/// <summary>
/// Reads bounded big-endian values from a TrueType or OpenType byte buffer.
/// </summary>
public sealed class TrueTypeReader
{
    private readonly ReadOnlyMemory<byte> _data;
    private int _position;

    /// <summary>
    /// Initializes a reader over the supplied font data.
    /// </summary>
    /// <param name="data">The bytes available to this reader.</param>
    public TrueTypeReader(ReadOnlyMemory<byte> data)
    {
        _data = data;
    }

    /// <summary>
    /// Gets the current position in the byte buffer.
    /// </summary>
    public int Position => _position;

    /// <summary>
    /// Gets the number of bytes in the byte buffer.
    /// </summary>
    public int Length => _data.Length;

    /// <summary>
    /// Reads one unsigned byte and advances the current position.
    /// </summary>
    /// <returns>The byte at the current position.</returns>
    public byte ReadUInt8() => ReadNext(1)[0];

    /// <summary>
    /// Reads a 16-bit unsigned big-endian integer and advances the current position.
    /// </summary>
    /// <returns>The decoded integer.</returns>
    public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16BigEndian(ReadNext(sizeof(ushort)));

    /// <summary>
    /// Reads a 16-bit signed big-endian integer and advances the current position.
    /// </summary>
    /// <returns>The decoded integer.</returns>
    public short ReadInt16() => BinaryPrimitives.ReadInt16BigEndian(ReadNext(sizeof(short)));

    /// <summary>
    /// Reads a 32-bit unsigned big-endian integer and advances the current position.
    /// </summary>
    /// <returns>The decoded integer.</returns>
    public uint ReadUInt32() => BinaryPrimitives.ReadUInt32BigEndian(ReadNext(sizeof(uint)));

    /// <summary>
    /// Reads a 32-bit signed big-endian integer and advances the current position.
    /// </summary>
    /// <returns>The decoded integer.</returns>
    public int ReadInt32() => BinaryPrimitives.ReadInt32BigEndian(ReadNext(sizeof(int)));

    /// <summary>
    /// Reads a four-byte printable ASCII table tag and advances the current position.
    /// </summary>
    /// <returns>The four-character tag.</returns>
    /// <exception cref="InvalidDataException">A tag contains a non-printable byte.</exception>
    public string ReadTag()
    {
        var bytes = ReadNext(4);
        for (var index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] is < 0x20 or > 0x7E)
                throw new InvalidDataException("An SFNT table tag contains a non-printable byte.");
        }

        return Encoding.ASCII.GetString(bytes);
    }

    /// <summary>
    /// Returns a bounded view of the byte buffer without changing the current position.
    /// </summary>
    /// <param name="offset">The absolute byte offset from the start of the buffer.</param>
    /// <param name="length">The number of bytes to include.</param>
    /// <returns>The requested memory region.</returns>
    public ReadOnlyMemory<byte> Slice(int offset, int length)
    {
        ValidateRange(offset, length);
        return _data.Slice(offset, length);
    }

    /// <summary>
    /// Returns the next byte span and advances the current position.
    /// </summary>
    /// <param name="length">The number of bytes to consume.</param>
    /// <returns>The requested bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The requested bytes exceed the buffer.</exception>
    private ReadOnlySpan<byte> ReadNext(int length)
    {
        ValidateRange(_position, length);
        var bytes = _data.Span.Slice(_position, length);
        _position += length;
        return bytes;
    }

    /// <summary>
    /// Verifies that a range lies within the backing buffer.
    /// </summary>
    /// <param name="offset">The absolute starting offset.</param>
    /// <param name="length">The requested byte count.</param>
    /// <exception cref="ArgumentOutOfRangeException">The range exceeds the buffer.</exception>
    private void ValidateRange(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > _data.Length - length)
            throw new ArgumentOutOfRangeException(nameof(offset), "The requested range exceeds the byte buffer.");
    }
}