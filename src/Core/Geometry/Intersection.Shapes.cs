using Nexus.Core.Shapes;

namespace Nexus.Core.Geometry;

public static partial class Intersection
{
    /// <summary>
    /// Tests the finite set of shape components symmetrically. Pairs containing a circle
    /// use XY projection, as do the explicit 2D query overloads.
    /// </summary>
    /// <exception cref="NotSupportedException">A component is not a supported primitive.</exception>
    public static bool Intersects(IShape first, IShape second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return (first, second) switch
        {
            (AxisAlignedBoundingBox a, AxisAlignedBoundingBox b) => Intersects(a.Bounds, b.Bounds),
            (Sphere a, Sphere b) => Intersects(a.Bounds, b.Bounds),
            (Circle a, Circle b) => Intersects(a.WorldGeometry, b.Bounds),
            (OrientedBoundingBox a, OrientedBoundingBox b) => Intersects(a.WorldGeometry, b.WorldGeometry),
            (AxisAlignedBoundingBox a, Sphere b) => Intersects(a.Bounds, b.Bounds),
            (Sphere a, AxisAlignedBoundingBox b) => Intersects(b.Bounds, a.Bounds),
            (AxisAlignedBoundingBox a, Circle b) => Intersects(a.Bounds, b.Bounds),
            (Circle a, AxisAlignedBoundingBox b) => Intersects(b.Bounds, a.Bounds),
            (Sphere a, Circle b) => Intersects(a.Bounds, b.Bounds),
            (Circle a, Sphere b) => Intersects(b.Bounds, a.Bounds),
            (OrientedBoundingBox a, AxisAlignedBoundingBox b) => Intersects(a.WorldGeometry, b.Bounds),
            (AxisAlignedBoundingBox a, OrientedBoundingBox b) => Intersects(b.WorldGeometry, a.Bounds),
            (OrientedBoundingBox a, Sphere b) => Intersects(a.WorldGeometry, b.Bounds),
            (Sphere a, OrientedBoundingBox b) => Intersects(b.WorldGeometry, a.Bounds),
            (OrientedBoundingBox a, Circle b) => Intersects(a.WorldGeometry, b.Bounds),
            (Circle a, OrientedBoundingBox b) => Intersects(b.WorldGeometry, a.Bounds),
            (Capsule a, Capsule b) => Intersects(a.Bounds, b.Bounds),
            (Capsule a, AxisAlignedBoundingBox b) => Intersects(a.Bounds, b.Bounds),
            (AxisAlignedBoundingBox a, Capsule b) => Intersects(b.Bounds, a.Bounds),
            (Capsule a, Sphere b) => Intersects(a.Bounds, b.Bounds),
            (Sphere a, Capsule b) => Intersects(b.Bounds, a.Bounds),
            (Capsule a, Circle b) => Intersects(a.Bounds, b.Bounds),
            (Circle a, Capsule b) => Intersects(b.Bounds, a.Bounds),
            (Capsule a, OrientedBoundingBox b) => Intersects(a.Bounds, b.WorldGeometry),
            (OrientedBoundingBox a, Capsule b) => Intersects(b.Bounds, a.WorldGeometry),
            _ => throw new NotSupportedException($"Unsupported shape pair: {first.GetType().Name}, {second.GetType().Name}."),
        };
    }
}
