using Nexus.Core.Geometry;
using MathCircle = Silk.NET.Maths.Circle<float>;
using MathSphere = Silk.NET.Maths.Sphere<float>;

namespace Nexus.Core.Shapes;

/// <summary>Defines a spherical shape transformed by its owner's world transform.</summary>
public partial class Sphere : Component, IShape
{
    [Observable]
    private Vector3D<float> _centerOffset;

    [Observable]
    private float _radius;

    /// <summary>Creates a zero-radius sphere at the owner's origin.</summary>
    public Sphere() { }

    /// <summary>Creates a sphere from a local center offset and radius.</summary>
    public Sphere(Vector3D<float> centerOffset, float radius)
    {
        CenterOffset = centerOffset;
        Radius = radius;
    }

    /// <summary>Gets the sphere transformed into world space.</summary>
    public MathSphere Bounds
    {
        get
        {
            var transform = GetWorldTransform();
            return new MathSphere(
                TransformPoint(CenterOffset, transform),
                MathF.Abs(Radius) * GetMaximumScale(transform)
            );
        }
    }

    /// <inheritdoc/>
    public bool IntersectsWith(Ray3D<float> value) => Intersection.Intersects(Bounds, value);

    /// <inheritdoc/>
    public bool IntersectsWith(Box2D<float> value) => Intersection.Intersects(Bounds, value);

    /// <inheritdoc/>
    public bool IntersectsWith(Box3D<float> value) => Intersection.Intersects(Bounds, value);

    /// <inheritdoc/>
    public bool IntersectsWith(Rectangle<float> value) => Intersection.Intersects(Bounds, value);

    /// <inheritdoc/>
    public bool IntersectsWith(MathCircle value) => Intersection.Intersects(Bounds, value);

    /// <inheritdoc/>
    public bool IntersectsWith(MathSphere value) => Intersection.Intersects(Bounds, value);

    /// <inheritdoc/>
    public bool IntersectsWith(Ray3D<float> ray, out float distance) => Intersection.Intersects(Bounds, ray, out distance);

    /// <inheritdoc/>
    public bool IntersectsWith(IShape other) => Intersection.Intersects(this, other);

    private Matrix4X4<float> GetWorldTransform() =>
        Owner is ISpatialObject spatial ? spatial.WorldTransform : Matrix4X4<float>.Identity;

    internal static Vector3D<float> TransformPoint(
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

    internal static float GetMaximumScale(Matrix4X4<float> transform)
    {
        var xx = transform.M11 * transform.M11 + transform.M12 * transform.M12 + transform.M13 * transform.M13;
        var yy = transform.M21 * transform.M21 + transform.M22 * transform.M22 + transform.M23 * transform.M23;
        var zz = transform.M31 * transform.M31 + transform.M32 * transform.M32 + transform.M33 * transform.M33;
        var xy = transform.M11 * transform.M21 + transform.M12 * transform.M22 + transform.M13 * transform.M23;
        var xz = transform.M11 * transform.M31 + transform.M12 * transform.M32 + transform.M13 * transform.M33;
        var yz = transform.M21 * transform.M31 + transform.M22 * transform.M32 + transform.M23 * transform.M33;
        var offDiagonalSquared = xy * xy + xz * xz + yz * yz;
        var mean = (xx + yy + zz) / 3f;

        float maximumEigenvalue;
        if (offDiagonalSquared == 0f)
        {
            maximumEigenvalue = MathF.Max(xx, MathF.Max(yy, zz));
        }
        else
        {
            var diagonalVariance =
                (xx - mean) * (xx - mean)
                + (yy - mean) * (yy - mean)
                + (zz - mean) * (zz - mean);
            var scale = MathF.Sqrt((diagonalVariance + 2f * offDiagonalSquared) / 6f);
            var a = (xx - mean) / scale;
            var d = (yy - mean) / scale;
            var f = (zz - mean) / scale;
            var b = xy / scale;
            var c = xz / scale;
            var e = yz / scale;
            var determinant =
                a * d * f + 2f * b * c * e - a * e * e - d * c * c - f * b * b;
            var angle = MathF.Acos(Math.Clamp(determinant / 2f, -1f, 1f)) / 3f;
            maximumEigenvalue = mean + 2f * scale * MathF.Cos(angle);
        }

        return MathF.Sqrt(MathF.Max(0f, maximumEigenvalue));
    }

}
