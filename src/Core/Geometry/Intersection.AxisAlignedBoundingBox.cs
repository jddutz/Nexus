using MathCircle = Silk.NET.Maths.Circle<float>;
using MathSphere = Silk.NET.Maths.Sphere<float>;

namespace Nexus.Core.Geometry;

public static partial class Intersection
{
    internal static class AxisAlignedBoundingBoxQueries
    {
        public static bool IntersectsWith(Box3D<float> shape, Ray3D<float> ray) => IntersectsWith(shape, ray, out _);

        public static bool IntersectsWith(Box3D<float> shape, Ray3D<float> ray, out float distance)
        {
            var bounds = shape;
            var direction = ray.Direction;
            var directionLength = direction.Length;
            if (directionLength == 0f)
            {
                distance = 0f;
                return false;
            }

            var near = 0f;
            var far = float.PositiveInfinity;
            if (
                !IntersectAxis(ray.Origin.X, direction.X, bounds.Min.X, bounds.Max.X, ref near, ref far)
                || !IntersectAxis(
                    ray.Origin.Y,
                    direction.Y,
                    bounds.Min.Y,
                    bounds.Max.Y,
                    ref near,
                    ref far
                )
                || !IntersectAxis(
                    ray.Origin.Z,
                    direction.Z,
                    bounds.Min.Z,
                    bounds.Max.Z,
                    ref near,
                    ref far
                )
            )
            {
                distance = 0f;
                return false;
            }

            distance = near * directionLength;
            return true;
        }

        public static bool IntersectsWith(Box3D<float> shape, Box2D<float> box)
        {
            var bounds = shape;
            return
                bounds.Max.X >= box.Min.X
                && bounds.Min.X <= box.Max.X
                && bounds.Max.Y >= box.Min.Y
                && bounds.Min.Y <= box.Max.Y;
        }

        public static bool IntersectsWith(Box3D<float> shape, Rectangle<float> rect)
        {
            var bounds = shape;
            return
                bounds.Max.X >= rect.Origin.X
                && bounds.Min.X <= rect.Max.X
                && bounds.Max.Y >= rect.Origin.Y
                && bounds.Min.Y <= rect.Max.Y;
        }

        public static bool IntersectsWith(Box3D<float> shape, MathCircle circle) =>
            Distance.Squared(circle.Center, new Box2D<float>(new Vector2D<float>(shape.Min.X, shape.Min.Y), new Vector2D<float>(shape.Max.X, shape.Max.Y))) <= circle.Radius * circle.Radius;

        public static bool IntersectsWith(Box3D<float> shape, MathSphere sphere) =>
            Distance.Squared(sphere.Center, shape) <= sphere.Radius * sphere.Radius;

        public static bool IntersectsWith(Box3D<float> shape, Box3D<float> box)
        {
            var bounds = shape;
            return
                bounds.Max.X >= box.Min.X
                && bounds.Min.X <= box.Max.X
                && bounds.Max.Y >= box.Min.Y
                && bounds.Min.Y <= box.Max.Y
                && bounds.Max.Z >= box.Min.Z
                && bounds.Min.Z <= box.Max.Z;
        }

        internal static bool IntersectAxis(
            float origin,
            float direction,
            float minimum,
            float maximum,
            ref float near,
            ref float far
        )
        {
            if (direction == 0f)
                return origin >= minimum && origin <= maximum;

            var first = (minimum - origin) / direction;
            var second = (maximum - origin) / direction;
            if (first > second)
                (first, second) = (second, first);

            near = MathF.Max(near, first);
            far = MathF.Min(far, second);
            return near <= far;
        }

    }
}
