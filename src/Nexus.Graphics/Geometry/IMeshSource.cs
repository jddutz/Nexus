namespace Nexus.Graphics.Geometry;

public interface IMeshSource
{
    ResourceId Id { get; }
    ulong Count { get; }

    ReadOnlyMemory<byte> GetVertexData(
        VertexSemanticEnum[] inputs,
        VectorFormatEnum? positionFormat = null,
        ColorFormatEnum? colorFormat = null
    );
}
