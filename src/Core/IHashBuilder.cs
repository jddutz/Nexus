namespace Nexus.Core;

public interface IHashBuilder
{
    IHashBuilder Add(ulong value);
    IHashBuilder AddRange(IEnumerable<ulong> values);
    IHashBuilder Add(uint value);
    IHashBuilder Add(Guid value);
    IHashBuilder AddRange(IEnumerable<Guid> values);
    IHashBuilder AddRange(IEnumerable<uint> values);
    IHashBuilder Add(int value);
    IHashBuilder AddRange(IEnumerable<int> values);
    IHashBuilder Add(float value);
    IHashBuilder AddRange(IEnumerable<float> values);
    IHashBuilder Add(double value);
    IHashBuilder AddRange(IEnumerable<double> values);
    IHashBuilder Add(bool value);
    IHashBuilder AddRange(IEnumerable<bool> values);
    IHashBuilder Add(string value);
    IHashBuilder AddRange(IEnumerable<string> values);
    IHashBuilder Add(byte value);
    IHashBuilder Add(byte[] value);
    IHashBuilder AddRange(IEnumerable<byte> values);
    IHashBuilder AddRange(IEnumerable<byte[]> values);
    ulong Compute();
}
