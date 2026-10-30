using Nexus.Core.Geometry;

namespace Nexus.Core.Shapes;

/// <summary>A local segment swept through a radius and transformed into world space.</summary>
/// <remarks>Nonuniform scale and shear use a conservative enclosing circular radius.</remarks>
public partial class Capsule : Component, IShape
{
    [Observable]
    private Vector3D<float> _startOffset;

    [Observable]
    private Vector3D<float> _endOffset;

    [Observable]
    private float _radius;

    public Capsule() { }

    public Capsule(Vector3D<float> startOffset, Vector3D<float> endOffset, float radius)
    {
        StartOffset = startOffset;
        EndOffset = endOffset;
        Radius = radius;
    }

    /// <summary>Creates an XY segment; its world capsule supports both 2D and 3D queries.</summary>
    public Capsule(Vector2D<float> startOffset, Vector2D<float> endOffset, float radius)
        : this(new Vector3D<float>(startOffset, 0), new Vector3D<float>(endOffset, 0), radius) { }

    public CapsuleGeometry Bounds
    {
        get
        {
            var transform = Owner is ISpatialObject spatial ? spatial.WorldTransform : Matrix4X4<float>.Identity;
            return new(Sphere.TransformPoint(StartOffset, transform), Sphere.TransformPoint(EndOffset, transform), MathF.Abs(Radius) * Sphere.GetMaximumScale(transform));
        }
    }

    public bool IntersectsWith(IShape other) => Intersection.Intersects(this, other);
    public bool IntersectsWith(Ray3D<float> ray) => Intersection.Intersects(Bounds, ray);
    public bool IntersectsWith(Ray3D<float> ray, out float distance) => Intersection.Intersects(Bounds, ray, out distance);
    public bool IntersectsWith(Box2D<float> box) => Intersection.Intersects(Bounds, box);
    public bool IntersectsWith(Box3D<float> box) => Intersection.Intersects(Bounds, box);
    public bool IntersectsWith(Rectangle<float> rect) => Intersection.Intersects(Bounds, rect);
    public bool IntersectsWith(Silk.NET.Maths.Circle<float> circle) => Intersection.Intersects(Bounds, circle);
    public bool IntersectsWith(Silk.NET.Maths.Sphere<float> sphere) => Intersection.Intersects(Bounds, sphere);
}
