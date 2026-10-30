namespace Nexus.Core;

/// <summary>
/// Defines a geometric shape component that supports spatial
/// intersection queries independently of rendering and physics.
/// </summary>
/// <remarks>
/// Boundary contact counts as intersection. Ray distances are measured in world units;
/// a zero-length ray misses and an interior origin hits at zero. Two-dimensional inputs
/// and circle pairs use world XY projection. The shared dispatcher supports the finite
/// primitive set in <see cref="Geometry.Intersection"/>.
/// </remarks>
public interface IShape : IComponent
{
    bool IntersectsWith(Ray3D<float> ray);
    bool IntersectsWith(Ray3D<float> ray, out float distance);
    bool IntersectsWith(Box2D<float> box);
    bool IntersectsWith(Box3D<float> box);
    bool IntersectsWith(Rectangle<float> rect);
    bool IntersectsWith(Circle<float> circle);
    bool IntersectsWith(Sphere<float> sphere);
    bool IntersectsWith(IShape other);
}
