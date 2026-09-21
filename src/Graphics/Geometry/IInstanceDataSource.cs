namespace Nexus.Graphics.Geometry;

public interface IInstanceDataSource
{
    DrawableId Id { get; }
    ulong Count { get; }

    ReadOnlyMemory<byte> GetInstanceData(ShaderInput[] layout);
}
