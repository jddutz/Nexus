namespace Nexus.Graphics.Geometry;

public interface IGeometryInstance
{
    IGeometry? Geometry { get; }
    Matrix4X4<float> TransformationMatrix { get; }
    Color Color { get; }
}
