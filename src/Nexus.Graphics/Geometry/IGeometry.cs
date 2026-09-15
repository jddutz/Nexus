namespace Nexus.Graphics.Geometry;

public interface IGeometry
{
    ResourceId Id { get; }
    string Name { get; }
    PrimitiveTopologyEnum Topology { get; }
    IGeometrySource Source { get; }
}
