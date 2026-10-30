using MathCircle = Silk.NET.Maths.Circle<float>;
using MathSphere = Silk.NET.Maths.Sphere<float>;

namespace Nexus.Core.Geometry;

public static partial class Intersection
{
    internal static class SphereQueries
    {
        public static bool IntersectsWith(MathSphere shape, Ray3D<float> ray) => IntersectsWith(shape, ray, out _);

        public static bool IntersectsWith(MathSphere shape, Ray3D<float> ray, out float distance)
        {
            var sphere = shape;
            var directionLength = ray.Direction.Length;
            if (directionLength == 0f)
            {
                distance = 0f;
                return false;
            }

            var offset = ray.Origin - sphere.Center;
            var quadratic = Vector3D.Dot(ray.Direction, ray.Direction);
            var linear = Vector3D.Dot(offset, ray.Direction);
            var constant = Vector3D.Dot(offset, offset) - sphere.Radius * sphere.Radius;
            if (constant <= 0f)
            {
                distance = 0f;
                return true;
            }

            var discriminant = linear * linear - quadratic * constant;
            if (discriminant < 0f)
            {
                distance = 0f;
                return false;
            }

            var root = MathF.Sqrt(discriminant);
            var fraction = (-linear - root) / quadratic;
            if (fraction < 0f)
                fraction = (-linear + root) / quadratic;

            if (fraction < 0f)
            {
                distance = 0f;
                return false;
            }

            distance = fraction * directionLength;
            return true;
        }

        public static bool IntersectsWith(MathSphere shape, Box2D<float> box) =>
            IntersectsWithRectangle(shape, box.Min, box.Max);

        public static bool IntersectsWith(MathSphere shape, Rectangle<float> rect) =>
            IntersectsWithRectangle(shape, rect.Origin, rect.Max);

        public static bool IntersectsWith(MathSphere shape, Box3D<float> box) => Intersection.Intersects(box, shape);

        public static bool IntersectsWith(MathSphere shape, MathCircle circle) =>
            CircleQueries.CirclesOverlap(
                new MathCircle(
                    new Vector2D<float>(shape.Center.X, shape.Center.Y),
                    shape.Radius
                ),
                circle
            );

        public static bool IntersectsWith(MathSphere shape, MathSphere sphere) => SpheresOverlap(shape, sphere);

        private static bool IntersectsWithRectangle(MathSphere shape, Vector2D<float> minimum, Vector2D<float> maximum) =>
            Distance.Squared(new Vector2D<float>(shape.Center.X, shape.Center.Y), new Box2D<float>(minimum, maximum)) <= shape.Radius * shape.Radius;

        private static bool SpheresOverlap(MathSphere first, MathSphere second)
        {
            var delta = first.Center - second.Center;
            var combinedRadius = MathF.Abs(first.Radius) + MathF.Abs(second.Radius);
            return delta.LengthSquared <= combinedRadius * combinedRadius;
        }

    }
}
