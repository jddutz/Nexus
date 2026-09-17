namespace Nexus.Graphics.Geometry;

public interface IMeshInstance
{
    Mesh Mesh { get; }
    Matrix4X4<float> TransformationMatrix { get; }
    Color Color { get; }
}
