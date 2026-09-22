namespace Tests;

using System.Text;
using Nexus.Core;

public class IdentityHashBuilderTests
{
    [Fact]
    public void Constructor_hashesSourceKind()
    {
        var builder = new IdentityHashBuilder("Test");

        Assert.Equal(HashBytes(Encoding.UTF8.GetBytes("Test")), builder.Compute());
    }

    [Fact]
    public void Constructor_withNullSourceKind_keepsOffsetBasis()
    {
        var builder = new IdentityHashBuilder(null!);

        Assert.Equal(IdentityHashBuilder.OffsetBasis, builder.Compute());
    }

    [Fact]
    public void Compute_onNewBuilder_returnsOffsetBasis()
    {
        Assert.Equal(IdentityHashBuilder.OffsetBasis, CreateBuilder().Compute());
    }

    [Fact]
    public void Add_usesFixedWidthLittleEndianEncoding()
    {
        const ulong value = 0x0102030405060708UL;
        var expected = HashBytes(0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01);

        Assert.Equal(expected, CreateBuilder().Add(value).Compute());
        Assert.Equal(HashBytes(0x04, 0x03, 0x02, 0x01), CreateBuilder().Add(0x01020304U).Compute());
        Assert.Equal(HashBytes(0xFF, 0xFF, 0xFF, 0xFF), CreateBuilder().Add(-1).Compute());
    }

    [Fact]
    public void Add_guid_usesGuidBytes()
    {
        var value = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

        Assert.Equal(HashBytes(value.ToByteArray()), CreateBuilder().Add(value).Compute());
    }

    [Fact]
    public void Add_floatAndDouble_useIeee754Bytes()
    {
        const float single = -123.5f;
        const double doubleValue = 9876.25;

        Assert.Equal(
            HashBytes(BitConverter.GetBytes(single)),
            CreateBuilder().Add(single).Compute()
        );
        Assert.Equal(
            HashBytes(BitConverter.GetBytes(doubleValue)),
            CreateBuilder().Add(doubleValue).Compute()
        );
    }

    [Fact]
    public void Add_bool_usesDistinctRepresentations()
    {
        var falseHash = CreateBuilder().Add(false).Compute();
        var trueHash = CreateBuilder().Add(true).Compute();

        Assert.NotEqual(falseHash, trueHash);
        Assert.NotEqual(IdentityHashBuilder.OffsetBasis, falseHash);
        Assert.NotEqual(IdentityHashBuilder.OffsetBasis, trueHash);
    }

    [Fact]
    public void Add_string_usesUtf8Bytes()
    {
        const string value = "Nexus\u00a9";

        Assert.Equal(
            HashBytes(Encoding.UTF8.GetBytes(value)),
            CreateBuilder().Add(value).Compute()
        );
        Assert.Equal(CreateBuilder().Compute(), CreateBuilder().Add(string.Empty).Compute());
    }

    [Fact]
    public void Add_byteArray_hashesBytesInOrder()
    {
        byte[] value = [0, 1, 255, 42];

        Assert.Equal(HashBytes(value), CreateBuilder().Add(value).Compute());
    }

    [Fact]
    public void Add_nullValues_areNoOp()
    {
        var builder = CreateBuilder();
        var initialHash = builder.Compute();

        builder.Add((string)null!);
        builder.Add((byte[])null!);

        Assert.Equal(initialHash, builder.Compute());
    }

    [Fact]
    public void AddRange_allScalarOverloads_matchIndividualAdds()
    {
        var guidValues = new[] { Guid.Empty, Guid.Parse("00112233-4455-6677-8899-aabbccddeeff") };
        var expected = CreateBuilder()
            .Add((ulong)7)
            .Add((uint)8)
            .Add(-9)
            .Add(guidValues[0])
            .Add(guidValues[1])
            .Add(1.25f)
            .Add(-2.5f)
            .Add(3.5)
            .Add(-4.5)
            .Add(true)
            .Add(false)
            .Compute();

        var actual = CreateBuilder()
            .AddRange(new ulong[] { 7 })
            .AddRange(new uint[] { 8 })
            .AddRange(new[] { -9 })
            .AddRange(guidValues)
            .AddRange(new[] { 1.25f, -2.5f })
            .AddRange(new[] { 3.5, -4.5 })
            .AddRange(new[] { true, false })
            .Compute();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AddRange_stringAndByteArrayOverloads_matchIndividualAdds()
    {
        var byteArrays = new byte[][] { [1, 2], null!, [], [3] };
        var expected = CreateBuilder()
            .Add("one")
            .Add((string)null!)
            .Add("two")
            .Add(byteArrays[0])
            .Add((byte[])null!)
            .Add(byteArrays[2])
            .Add(byteArrays[3])
            .Add((byte)4)
            .Add((byte)5)
            .Compute();

        var actual = CreateBuilder()
            .AddRange(new string[] { "one", null!, "two" })
            .AddRange(byteArrays)
            .AddRange(new byte[] { 4, 5 })
            .Compute();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AddRange_nullValues_areNoOp()
    {
        var builder = CreateBuilder();
        var initialHash = builder.Compute();

        builder.AddRange((IEnumerable<ulong>)null!);
        builder.AddRange((IEnumerable<uint>)null!);
        builder.AddRange((IEnumerable<int>)null!);
        builder.AddRange((IEnumerable<Guid>)null!);
        builder.AddRange((IEnumerable<float>)null!);
        builder.AddRange((IEnumerable<double>)null!);
        builder.AddRange((IEnumerable<bool>)null!);
        builder.AddRange((IEnumerable<string>)null!);
        builder.AddRange((IEnumerable<byte>)null!);
        builder.AddRange((IEnumerable<byte[]>)null!);

        Assert.Equal(initialHash, builder.Compute());
    }

    [Fact]
    public void AddMethods_returnTheSameBuilderInstance()
    {
        var builder = CreateBuilder();

        Assert.Same(builder, builder.Add((ulong)1));
        Assert.Same(builder, builder.Add((uint)1));
        Assert.Same(builder, builder.Add(1));
        Assert.Same(builder, builder.Add(Guid.Empty));
        Assert.Same(builder, builder.Add(1f));
        Assert.Same(builder, builder.Add(1d));
        Assert.Same(builder, builder.Add(true));
        Assert.Same(builder, builder.Add("value"));
        Assert.Same(builder, builder.Add([1]));
        Assert.Same(builder, builder.AddRange(Array.Empty<ulong>()));
    }

    private static IdentityHashBuilder CreateBuilder() => new(null!);

    private static ulong HashBytes(params byte[] bytes)
    {
        var hash = IdentityHashBuilder.OffsetBasis;

        foreach (var value in bytes)
        {
            hash ^= value;
            hash *= IdentityHashBuilder.Prime;
        }

        return hash;
    }
}
