using MathCircle = Silk.NET.Maths.Circle<float>;
using MathSphere = Silk.NET.Maths.Sphere<float>;

namespace Nexus.Core.Geometry;

public static partial class Intersection
{
    internal static class CircleQueries
    {
        public static bool IntersectsWith(CircleGeometry shape, Ray3D<float> ray) => IntersectsWith(shape, ray, out _);

        public static bool IntersectsWith(CircleGeometry shape, Ray3D<float> ray, out float distance)
        {
            var bounds = shape.Bounds;
            var worldZ = shape.Z;
            var directionLength = ray.Direction.Length;
            if (directionLength == 0f)
            {
                distance = 0f;
                return false;
            }

            if (ray.Direction.Z != 0f)
            {
                var fraction = (worldZ - ray.Origin.Z) / ray.Direction.Z;
                if (fraction < 0f)
                {
                    distance = 0f;
                    return false;
                }

                var hitX = ray.Origin.X + ray.Direction.X * fraction - bounds.Center.X;
                var hitY = ray.Origin.Y + ray.Direction.Y * fraction - bounds.Center.Y;
                var radiusSquared = bounds.Radius * bounds.Radius;
                if (hitX * hitX + hitY * hitY <= radiusSquared)
                {
                    distance = fraction * directionLength;
                    return true;
                }

                distance = 0f;
                return false;
            }

            if (ray.Origin.Z != worldZ)
            {
                distance = 0f;
                return false;
            }

            // A coplanar ray meets the same circular section of a sphere.
            return Intersection.Intersects(
                new MathSphere(new Vector3D<float>(bounds.Center, worldZ), bounds.Radius),
                ray,
                out distance
            );
        }

        public static bool IntersectsWith(CircleGeometry shape, Box2D<float> box) =>
            IntersectsWithRectangle(shape, box.Min, box.Max);

        public static bool IntersectsWith(CircleGeometry shape, Rectangle<float> rect) =>
            IntersectsWithRectangle(shape, rect.Origin, rect.Max);

        public static bool IntersectsWith(CircleGeometry shape, Box3D<float> box) =>
            IntersectsWithRectangle(shape,
                new Vector2D<float>(box.Min.X, box.Min.Y),
                new Vector2D<float>(box.Max.X, box.Max.Y)
            );

        public static bool IntersectsWith(CircleGeometry shape, MathCircle circle) => CirclesOverlap(shape.Bounds, circle);

        public static bool IntersectsWith(CircleGeometry shape, MathSphere sphere) =>
            CirclesOverlap(
                shape.Bounds,
                new MathCircle(
                    new Vector2D<float>(sphere.Center.X, sphere.Center.Y),
                    MathF.Abs(sphere.Radius)
                )
            );

        private static bool IntersectsWithRectangle(CircleGeometry shape, Vector2D<float> minimum, Vector2D<float> maximum) =>
            Distance.Squared(shape.Bounds.Center, new Box2D<float>(minimum, maximum)) <= shape.Bounds.Radius * shape.Bounds.Radius;

        internal static bool CirclesOverlap(MathCircle first, MathCircle second)
        {
            var delta = first.Center - second.Center;
            var combinedRadius = MathF.Abs(first.Radius) + MathF.Abs(second.Radius);
            return delta.LengthSquared <= combinedRadius * combinedRadius;
        }

    }
}
