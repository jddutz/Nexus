using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Nexus.Assets.Typography.FontReader.OpenType;

/// <summary>Bounded CFF INDEX, DICT, and numeric decoding.</summary>
internal sealed class CffDataReader(ReadOnlyMemory<byte> data, int position = 0)
{
    internal int Position { get; private set; } = position;
    internal int Length => data.Length;
    internal byte Byte()
    {
        if ((uint)Position >= (uint)data.Length)
            throw new InvalidDataException("Truncated CFF data.");
        return data.Span[Position++];
    }
    internal int Unsigned(int size)
    {
        long value = 0;
        for (var i = 0; i < size; i++) value = (value << 8) | Byte();
        if (value > int.MaxValue) throw new InvalidDataException("CFF offset exceeds supported size.");
        return (int)value;
    }
    internal ReadOnlyMemory<byte> Slice(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > data.Length - length)
            throw new InvalidDataException("CFF range exceeds the table.");
        return data.Slice(offset, length);
    }
    internal ReadOnlyMemory<byte>[] Index()
    {
        var count = Unsigned(2);
        if (count == 0) return [];
        var size = Byte();
        if (size is < 1 or > 4) throw new InvalidDataException("Invalid CFF INDEX offset size.");
        var offsets = new int[count + 1];
        for (var i = 0; i <= count; i++) offsets[i] = Unsigned(size);
        if (offsets[0] != 1) throw new InvalidDataException("CFF INDEX must start at offset one.");
        var start = Position;
        if (offsets[count] < 1 || (long)start + offsets[count] - 1 > data.Length)
            throw new InvalidDataException("CFF INDEX data exceeds the table.");
        var result = new ReadOnlyMemory<byte>[count];
        for (var i = 0; i < count; i++)
        {
            if (offsets[i] < 1 || offsets[i + 1] < offsets[i]) throw new InvalidDataException("Unordered CFF INDEX offsets.");
            result[i] = Slice(checked(start + offsets[i] - 1), offsets[i + 1] - offsets[i]);
        }
        Position = checked(start + offsets[count] - 1);
        return result;
    }
    internal double Number(byte first, bool dict)
    {
        if (first is >= 32 and <= 246) return first - 139;
        if (first is >= 247 and <= 250) return (first - 247) * 256 + Byte() + 108;
        if (first is >= 251 and <= 254) return -(first - 251) * 256 - Byte() - 108;
        if (first == 28) return (short)((Byte() << 8) | Byte());
        if (first == 29 && dict || first == 255 && !dict)
        {
            var bytes = new byte[] { Byte(), Byte(), Byte(), Byte() };
            var value = BinaryPrimitives.ReadInt32BigEndian(bytes);
            return dict ? value : value / 65536d;
        }
        if (first == 30 && dict)
        {
            var text = new StringBuilder();
            while (true)
            {
                var b = Byte();
                foreach (var nibble in new[] { b >> 4, b & 15 })
                {
                    if (nibble == 15)
                    {
                        if (!double.TryParse(text.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                            || !double.IsFinite(value)) throw new InvalidDataException("Invalid CFF real number.");
                        return value;
                    }
                    text.Append(nibble switch { <= 9 => nibble.ToString(CultureInfo.InvariantCulture), 10 => ".", 11 => "E", 12 => "E-", 14 => "-", _ => throw new InvalidDataException("Invalid CFF real nibble.") });
                    if (text.Length > 64) throw new InvalidDataException("CFF real number is too long.");
                }
            }
        }
        throw new InvalidDataException("Invalid CFF number encoding.");
    }
    internal static Dictionary<int, double[]> Dict(ReadOnlyMemory<byte> data)
    {
        var reader = new CffDataReader(data);
        var result = new Dictionary<int, double[]>();
        var operands = new List<double>();
        while (reader.Position < reader.Length)
        {
            var b = reader.Byte();
            if (b >= 28)
            {
                operands.Add(reader.Number(b, true));
                if (operands.Count > 48) throw new InvalidDataException("CFF DICT stack overflow.");
                continue;
            }
            var op = b == 12 ? 1200 + reader.Byte() : b;
            result[op] = operands.ToArray();
            operands.Clear();
        }
        if (operands.Count != 0) throw new InvalidDataException("Unterminated CFF DICT operands.");
        return result;
    }
    internal static int Integer(double value)
    {
        if (!double.IsFinite(value) || value < 0 || value > int.MaxValue || value != Math.Truncate(value))
            throw new InvalidDataException("Invalid CFF integer offset or count.");
        return (int)value;
    }
}
