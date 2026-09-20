namespace Nexus.Graphics.Geometry;

public interface IInstanceDataSource
{
    ResourceId Id { get; }
    ulong Count { get; }

    ReadOnlyMemory<byte> GetInstanceData(InstanceLayout layout);
}
