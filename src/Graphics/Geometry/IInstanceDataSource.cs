namespace Nexus.Graphics.Geometry;

public interface IInstanceDataSource
{
    GraphicsId Id { get; }
    ulong Count { get; }

    ReadOnlyMemory<byte> GetInstanceData(ShaderInput[] layout);
}
