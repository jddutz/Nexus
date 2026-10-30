using Nexus.Core.Geometry;
using MathCircle = Silk.NET.Maths.Circle<float>;
using MathSphere = Silk.NET.Maths.Sphere<float>;

namespace Nexus.Core.Shapes;

/// <summary>Defines a circular shape in the owner's world-space XY plane.</summary>
public partial class Circle : Component, IShape
{
    [Observable]
    private Vector2D<float> _centerOffset;

    [Observable]
    private float _radius;

    /// <summary>Creates a zero-radius circle at the owner's origin.</summary>
    public Circle() { }

    /// <summary>Creates a circle from a local center offset and radius.</summary>
    public Circle(Vector2D<float> centerOffset, float radius)
    {
        CenterOffset = centerOffset;
        Radius = radius;
    }

    /// <summary>Gets the circle transformed into world space.</summary>
    public MathCircle Bounds
    {
        get
        {
            var transform = GetWorldTransform();
            var center = TransformPoint(
                new Vector3D<float>(CenterOffset.X, CenterOffset.Y, 0f),
                transform
            );
            return new MathCircle(
                new Vector2D<float>(center.X, center.Y),
                MathF.Abs(Radius) * GetMaximumProjectedScale(transform)
            );
        }
    }

    private float WorldZ
    {
        get
        {
            var center = TransformPoint(
                new Vector3D<float>(CenterOffset.X, CenterOffset.Y, 0f),
                GetWorldTransform()
            );
            return center.Z;
        }
    }

    /// <summary>Gets an independent world-space circle snapshot.</summary>
    public CircleGeometry WorldGeometry => new(Bounds, WorldZ);

    /// <inheritdoc/>
    public bool IntersectsWith(Ray3D<float> value) => Intersection.Intersects(WorldGeometry, value);

    /// <inheritdoc/>
    public bool IntersectsWith(Box2D<float> value) => Intersection.Intersects(WorldGeometry, value);

    /// <inheritdoc/>
    public bool IntersectsWith(Box3D<float> value) => Intersection.Intersects(WorldGeometry, value);

    /// <inheritdoc/>
    public bool IntersectsWith(Rectangle<float> value) => Intersection.Intersects(WorldGeometry, value);

    /// <inheritdoc/>
    public bool IntersectsWith(MathCircle value) => Intersection.Intersects(WorldGeometry, value);

    /// <inheritdoc/>
    public bool IntersectsWith(MathSphere value) => Intersection.Intersects(WorldGeometry, value);

    /// <inheritdoc/>
    public bool IntersectsWith(Ray3D<float> ray, out float distance) => Intersection.Intersects(WorldGeometry, ray, out distance);

    /// <inheritdoc/>
    public bool IntersectsWith(IShape other) => Intersection.Intersects(this, other);

    private Matrix4X4<float> GetWorldTransform() =>
        Owner is ISpatialObject spatial ? spatial.WorldTransform : Matrix4X4<float>.Identity;

    private static Vector3D<float> TransformPoint(
        Vector3D<float> point,
        Matrix4X4<float> transform
    )
    {
        var transformed = Vector4D.Transform(new Vector4D<float>(point, 1f), transform);
        return new Vector3D<float>(
            transformed.X / transformed.W,
            transformed.Y / transformed.W,
            transformed.Z / transformed.W
        );
    }

    private static float GetMaximumProjectedScale(Matrix4X4<float> transform)
    {
        var a = transform.M11;
        var b = transform.M12;
        var c = transform.M21;
        var d = transform.M22;
        var trace = a * a + b * b + c * c + d * d;
        var determinant = a * d - b * c;
        var discriminant = MathF.Max(0f, trace * trace - 4f * determinant * determinant);
        return MathF.Sqrt((trace + MathF.Sqrt(discriminant)) / 2f);
    }

}
