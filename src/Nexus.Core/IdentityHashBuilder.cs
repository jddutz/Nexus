namespace Nexus.Core;

/// <summary>
/// Fast non-cryptographic fingerprint builder used for data synchronization
/// and change detection.
///
/// Based on Fowler–Noll–Vo hash function (FNV-1a):
/// https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
///
/// NOTES:
///
/// Being an iterative hash based primarily on multiplication and XOR,
/// the algorithm is sensitive to the number zero. Specifically, if the
/// hash value were to become zero at any point during calculation, and
/// the next byte hashed were also all zeroes, then the hash would not
/// change. This makes colliding messages trivial to create given a
/// message that results in a hash value of zero at some point in its
/// calculation. Additional operations, such as the addition of a third
/// constant prime on each step, can mitigate this but may have
/// detrimental effects on avalanche effect or random distribution of
/// hash values.
///
/// This is NOT a cryptographically-secure hash algorithm, it is
/// optimized for hash table and checksum computation which is suitable
/// for our use case.
///
/// </summary>
public class IdentityHashBuilder : IHashBuilder
{
    public const ulong OffsetBasis = 14695981039346656037UL;
    public const ulong Prime = 1099511628211UL;

    private ulong _hash = OffsetBasis;

    public IdentityHashBuilder(string sourceKind)
    {
        Add(sourceKind);
    }

    /// <summary>
    /// Adds a value to the fingerprint in its fixed-width little-endian representation.
    /// Values are therefore hashed in the order in which they are added.
    /// </summary>
    public IHashBuilder Add(ulong value)
    {
        for (var index = 0; index < sizeof(ulong); index++)
        {
            Add((byte)(value >> (index * 8)));
        }

        return this;
    }

    /// <summary>Adds unsigned integer values to the fingerprint in their supplied order.</summary>
    public IHashBuilder AddRange(IEnumerable<ulong> values)
    {
        if (values == null)
        {
            return this;
        }

        foreach (var value in values)
        {
            Add(value);
        }

        return this;
    }

    /// <summary>Adds an unsigned integer value in its fixed-width little-endian representation.</summary>
    public IHashBuilder Add(uint value)
    {
        for (var index = 0; index < sizeof(uint); index++)
        {
            Add((byte)(value >> (index * 8)));
        }

        return this;
    }

    /// <summary>Adds a GUID using its fixed-width byte representation.</summary>
    public IHashBuilder Add(Guid value) => Add(value.ToByteArray());

    /// <summary>Adds GUID values to the fingerprint in their supplied order.</summary>
    public IHashBuilder AddRange(IEnumerable<Guid> values)
    {
        if (values == null)
        {
            return this;
        }

        foreach (var value in values)
        {
            Add(value);
        }

        return this;
    }

    /// <summary>Adds unsigned integer values to the fingerprint in their supplied order.</summary>
    public IHashBuilder AddRange(IEnumerable<uint> values)
    {
        if (values == null)
        {
            return this;
        }

        foreach (var value in values)
        {
            Add(value);
        }

        return this;
    }

    /// <summary>Adds an integer value in its fixed-width little-endian representation.</summary>
    public IHashBuilder Add(int value) => Add(unchecked((uint)value));

    /// <summary>Adds integer values to the fingerprint in their supplied order.</summary>
    public IHashBuilder AddRange(IEnumerable<int> values)
    {
        if (values == null)
        {
            return this;
        }

        foreach (var value in values)
        {
            Add(value);
        }

        return this;
    }

    /// <summary>Adds a single-precision value using its IEEE 754 representation.</summary>
    public IHashBuilder Add(float value) => AddLittleEndian(BitConverter.GetBytes(value));

    /// <summary>Adds single-precision values to the fingerprint in their supplied order.</summary>
    public IHashBuilder AddRange(IEnumerable<float> values)
    {
        if (values == null)
        {
            return this;
        }

        foreach (var value in values)
        {
            Add(value);
        }

        return this;
    }

    /// <summary>Adds a double-precision value using its IEEE 754 representation.</summary>
    public IHashBuilder Add(double value) => AddLittleEndian(BitConverter.GetBytes(value));

    /// <summary>Adds double-precision values to the fingerprint in their supplied order.</summary>
    public IHashBuilder AddRange(IEnumerable<double> values)
    {
        if (values == null)
        {
            return this;
        }

        foreach (var value in values)
        {
            Add(value);
        }

        return this;
    }

    /// <summary>Adds a UTF-8 string to the fingerprint.</summary>
    public IHashBuilder Add(string value)
    {
        if (value == null)
        {
            return this;
        }

        return Add(Encoding.UTF8.GetBytes(value));
    }

    /// <summary>Adds UTF-8 strings to the fingerprint in their supplied order.</summary>
    public IHashBuilder AddRange(IEnumerable<string> values)
    {
        if (values == null)
        {
            return this;
        }

        foreach (var value in values)
        {
            Add(value);
        }

        return this;
    }

    /// <summary>Adds bytes to the fingerprint in their supplied order.</summary>
    public IHashBuilder Add(byte[] value)
    {
        if (value == null)
        {
            return this;
        }

        foreach (var item in value)
        {
            Add(item);
        }

        return this;
    }

    /// <summary>Adds unsigned integer values to the fingerprint in their supplied order.</summary>
    public IHashBuilder AddRange(IEnumerable<byte> values)
    {
        if (values == null)
        {
            return this;
        }

        foreach (var value in values)
        {
            Add(value);
        }

        return this;
    }

    /// <summary>Adds bytes to the fingerprint in their supplied order.</summary>
    public IHashBuilder AddRange(IEnumerable<byte[]> values)
    {
        if (values == null)
        {
            return this;
        }

        foreach (var value in values)
        {
            Add(value);
        }

        return this;
    }

    /// <summary>Returns the current FNV-1a 64-bit fingerprint.</summary>
    public ulong Compute() => _hash;

    private void Add(byte value)
    {
        _hash ^= value;
        _hash *= Prime;
    }

    private IHashBuilder AddLittleEndian(byte[] value)
    {
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(value);
        }

        return Add(value);
    }
}
