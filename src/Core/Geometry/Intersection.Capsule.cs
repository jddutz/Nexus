namespace Nexus.Core.Geometry;

public static partial class Intersection
{
    public static bool Intersects(CapsuleGeometry capsule, Sphere<float> sphere)
    {
        var radius = MathF.Abs(capsule.Radius) + MathF.Abs(sphere.Radius);
        return Distance.Squared(sphere.Center, capsule.Start, capsule.End) <= radius * radius;
    }

    public static bool Intersects(CapsuleGeometry first, CapsuleGeometry second)
    {
        var radius = MathF.Abs(first.Radius) + MathF.Abs(second.Radius);
        return Distance.Squared(first.Start, first.End, second.Start, second.End) <= radius * radius;
    }

    public static bool Intersects(CapsuleGeometry capsule, Box3D<float> box) =>
        Distance.Squared(capsule.Start, capsule.End, box) <= capsule.Radius * capsule.Radius;

    public static bool Intersects(CapsuleGeometry capsule, Box2D<float> box) =>
        Distance.Squared(XY(capsule.Start), XY(capsule.End), box) <= capsule.Radius * capsule.Radius;

    public static bool Intersects(CapsuleGeometry capsule, Rectangle<float> rect) =>
        Intersects(capsule, new Box2D<float>(rect.Origin, rect.Max));

    public static bool Intersects(CapsuleGeometry capsule, Circle<float> circle)
    {
        var radius = MathF.Abs(capsule.Radius) + MathF.Abs(circle.Radius);
        return Distance.Squared(circle.Center, XY(capsule.Start), XY(capsule.End)) <= radius * radius;
    }

    public static bool Intersects(CapsuleGeometry capsule, OrientedBox box) =>
        Distance.Squared(capsule.Start, capsule.End, box) <= capsule.Radius * capsule.Radius;

    public static bool Intersects(CapsuleGeometry capsule, Ray3D<float> ray) => Intersects(capsule, ray, out _);

    /// <summary>Finds the first hit in world units. Zero-length rays miss; interior origins hit at zero.</summary>
    public static bool Intersects(CapsuleGeometry capsule, Ray3D<float> ray, out float distance)
    {
        distance = 0;
        var length = ray.Direction.Length;
        if (length == 0) return false;
        var radius = MathF.Abs(capsule.Radius);
        if (Distance.Squared(ray.Origin, capsule.Start, capsule.End) <= radius * radius) return true;

        var closest = float.PositiveInfinity;
        if (Intersects(new Sphere<float>(capsule.Start, radius), ray, out var startHit)) closest = startHit;
        if (Intersects(new Sphere<float>(capsule.End, radius), ray, out var endHit)) closest = MathF.Min(closest, endHit);

        var segment = capsule.End - capsule.Start;
        var segmentLength = segment.Length;
        if (segmentLength > 0)
        {
            var axis = segment / segmentLength;
            var direction = ray.Direction / length;
            var offset = ray.Origin - capsule.Start;
            var axialDirection = Vector3D.Dot(direction, axis);
            var axialOrigin = Vector3D.Dot(offset, axis);
            var perpendicularDirection = direction - axis * axialDirection;
            var perpendicularOrigin = offset - axis * axialOrigin;
            var a = perpendicularDirection.LengthSquared;
            var b = Vector3D.Dot(perpendicularDirection, perpendicularOrigin);
            var c = perpendicularOrigin.LengthSquared - radius * radius;
            var discriminant = b * b - a * c;
            if (a > 0 && discriminant >= 0)
            {
                var root = MathF.Sqrt(discriminant);
                CheckCylinderRoot((-b - root) / a, axialOrigin, axialDirection, segmentLength, ref closest);
                CheckCylinderRoot((-b + root) / a, axialOrigin, axialDirection, segmentLength, ref closest);
            }
        }

        if (float.IsPositiveInfinity(closest)) return false;
        distance = closest;
        return true;
    }

    private static void CheckCylinderRoot(float t, float origin, float direction, float length, ref float closest)
    {
        var axial = origin + t * direction;
        if (t >= 0 && axial >= 0 && axial <= length) closest = MathF.Min(closest, t);
    }

    private static Vector2D<float> XY(Vector3D<float> point) => new(point.X, point.Y);
}
