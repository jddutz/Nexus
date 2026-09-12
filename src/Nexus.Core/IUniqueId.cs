namespace Nexus.Core;

public interface IUniqueId : IEquatable<IUniqueId>
{
    ulong Value { get; }
}
