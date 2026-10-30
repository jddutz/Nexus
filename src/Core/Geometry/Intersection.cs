using MathCircle = Silk.NET.Maths.Circle<float>;
using MathSphere = Silk.NET.Maths.Sphere<float>;
using Nexus.Core.Shapes;

namespace Nexus.Core.Geometry;

/// <summary>World-space intersection queries. Contact at a boundary counts as intersection.</summary>
public static partial class Intersection
{
    public static bool Intersects(Box3D<float> shape, Ray3D<float> value) => AxisAlignedBoundingBoxQueries.IntersectsWith(shape, value);
    public static bool Intersects(Box3D<float> shape, Box2D<float> value) => AxisAlignedBoundingBoxQueries.IntersectsWith(shape, value);
    public static bool Intersects(Box3D<float> shape, Box3D<float> value) => AxisAlignedBoundingBoxQueries.IntersectsWith(shape, value);
    public static bool Intersects(Box3D<float> shape, Rectangle<float> value) => AxisAlignedBoundingBoxQueries.IntersectsWith(shape, value);
    public static bool Intersects(Box3D<float> shape, MathCircle value) => AxisAlignedBoundingBoxQueries.IntersectsWith(shape, value);
    public static bool Intersects(Box3D<float> shape, MathSphere value) => AxisAlignedBoundingBoxQueries.IntersectsWith(shape, value);
    /// <summary>Returns the first hit distance in world units, or zero on a miss. An interior origin hits at zero.</summary>
    public static bool Intersects(Box3D<float> shape, Ray3D<float> ray, out float distance) => AxisAlignedBoundingBoxQueries.IntersectsWith(shape, ray, out distance);
    public static bool Intersects(CircleGeometry shape, Ray3D<float> value) => CircleQueries.IntersectsWith(shape, value);
    public static bool Intersects(CircleGeometry shape, Box2D<float> value) => CircleQueries.IntersectsWith(shape, value);
    public static bool Intersects(CircleGeometry shape, Box3D<float> value) => CircleQueries.IntersectsWith(shape, value);
    public static bool Intersects(CircleGeometry shape, Rectangle<float> value) => CircleQueries.IntersectsWith(shape, value);
    public static bool Intersects(CircleGeometry shape, MathCircle value) => CircleQueries.IntersectsWith(shape, value);
    public static bool Intersects(CircleGeometry shape, MathSphere value) => CircleQueries.IntersectsWith(shape, value);
    /// <summary>Returns the first hit distance in world units, or zero on a miss. An interior origin hits at zero.</summary>
    public static bool Intersects(CircleGeometry shape, Ray3D<float> ray, out float distance) => CircleQueries.IntersectsWith(shape, ray, out distance);
    public static bool Intersects(MathSphere shape, Ray3D<float> value) => SphereQueries.IntersectsWith(shape, value);
    public static bool Intersects(MathSphere shape, Box2D<float> value) => SphereQueries.IntersectsWith(shape, value);
    public static bool Intersects(MathSphere shape, Box3D<float> value) => SphereQueries.IntersectsWith(shape, value);
    public static bool Intersects(MathSphere shape, Rectangle<float> value) => SphereQueries.IntersectsWith(shape, value);
    public static bool Intersects(MathSphere shape, MathCircle value) => SphereQueries.IntersectsWith(shape, value);
    public static bool Intersects(MathSphere shape, MathSphere value) => SphereQueries.IntersectsWith(shape, value);
    /// <summary>Returns the first hit distance in world units, or zero on a miss. An interior origin hits at zero.</summary>
    public static bool Intersects(MathSphere shape, Ray3D<float> ray, out float distance) => SphereQueries.IntersectsWith(shape, ray, out distance);
    public static bool Intersects(OrientedBox shape, Ray3D<float> value) => OrientedBoundingBoxQueries.IntersectsWith(shape, value);
    public static bool Intersects(OrientedBox shape, Box2D<float> value) => OrientedBoundingBoxQueries.IntersectsWith(shape, value);
    public static bool Intersects(OrientedBox shape, Box3D<float> value) => OrientedBoundingBoxQueries.IntersectsWith(shape, value);
    public static bool Intersects(OrientedBox shape, Rectangle<float> value) => OrientedBoundingBoxQueries.IntersectsWith(shape, value);
    public static bool Intersects(OrientedBox shape, MathCircle value) => OrientedBoundingBoxQueries.IntersectsWith(shape, value);
    public static bool Intersects(OrientedBox shape, MathSphere value) => OrientedBoundingBoxQueries.IntersectsWith(shape, value);
    /// <summary>Returns the first hit distance in world units, or zero on a miss. An interior origin hits at zero.</summary>
    public static bool Intersects(OrientedBox shape, Ray3D<float> ray, out float distance) => OrientedBoundingBoxQueries.IntersectsWith(shape, ray, out distance);
    public static bool Intersects(OrientedBox first, OrientedBox second) => OrientedBoundingBoxQueries.IntersectsWith(first, second);
}
