using MathCircle = Silk.NET.Maths.Circle<float>;
using MathSphere = Silk.NET.Maths.Sphere<float>;

namespace Nexus.Core.Geometry;

public static partial class Intersection
{
    internal static class OrientedBoundingBoxQueries
    {
        public static bool IntersectsWith(OrientedBox shape, Ray3D<float> ray) => IntersectsWith(shape, ray, out _);

        public static bool IntersectsWith(OrientedBox shape, Ray3D<float> ray, out float distance)
        {
            var transform = shape.Transform;
            if (!Matrix4X4.Invert(transform, out var inverse))
            {
                distance = 0f;
                return false;
            }

            var localOrigin = TransformPoint(ray.Origin, inverse);
            var localDirection = TransformDirection(ray.Direction, inverse);
            if (ray.Direction.Length == 0f || localDirection.Length == 0f)
            {
                distance = 0f;
                return false;
            }

            var near = 0f;
            var far = float.PositiveInfinity;
            if (
                !AxisAlignedBoundingBoxQueries.IntersectAxis(
                    localOrigin.X,
                    localDirection.X,
                    shape.Minimum.X,
                    shape.Maximum.X,
                    ref near,
                    ref far
                )
                || !AxisAlignedBoundingBoxQueries.IntersectAxis(
                    localOrigin.Y,
                    localDirection.Y,
                    shape.Minimum.Y,
                    shape.Maximum.Y,
                    ref near,
                    ref far
                )
                || !AxisAlignedBoundingBoxQueries.IntersectAxis(
                    localOrigin.Z,
                    localDirection.Z,
                    shape.Minimum.Z,
                    shape.Maximum.Z,
                    ref near,
                    ref far
                )
            )
            {
                distance = 0f;
                return false;
            }

            distance = near * ray.Direction.Length;
            return true;
        }

        public static bool IntersectsWith(OrientedBox shape, Box2D<float> box)
        {
            return IntersectsWithProjectedRectangle(shape, box.Min, box.Max);
        }

        public static bool IntersectsWith(OrientedBox shape, Rectangle<float> rect)
        {
            return IntersectsWithProjectedRectangle(shape, rect.Origin, rect.Max);
        }

        public static bool IntersectsWith(OrientedBox shape, MathCircle circle)
        {
            var projectedCorners = GetWorldCorners(shape)
                .Select(corner => new Vector2D<float>(corner.X, corner.Y));
            var hull = GetConvexHull(projectedCorners);
            var center = circle.Center;

            if (hull.Count >= 3)
            {
                var isInside = true;
                for (var index = 0; index < hull.Count; index++)
                {
                    var first = hull[index];
                    var second = hull[(index + 1) % hull.Count];
                    var cross =
                        (second.X - first.X) * (center.Y - first.Y)
                        - (second.Y - first.Y) * (center.X - first.X);
                    if (cross < -1e-6f)
                    {
                        isInside = false;
                        break;
                    }
                }

                if (isInside)
                    return true;
            }

            var radiusSquared = circle.Radius * circle.Radius;
            for (var index = 0; index < hull.Count; index++)
            {
                var first = hull[index];
                var second = hull[(index + 1) % hull.Count];
                var edgeX = second.X - first.X;
                var edgeY = second.Y - first.Y;
                var edgeLengthSquared = edgeX * edgeX + edgeY * edgeY;
                var fraction =
                    edgeLengthSquared == 0f
                        ? 0f
                        : Math.Clamp(
                            ((center.X - first.X) * edgeX + (center.Y - first.Y) * edgeY)
                                / edgeLengthSquared,
                            0f,
                            1f
                        );
                var closestX = first.X + fraction * edgeX;
                var closestY = first.Y + fraction * edgeY;
                var deltaX = center.X - closestX;
                var deltaY = center.Y - closestY;
                if (deltaX * deltaX + deltaY * deltaY <= radiusSquared)
                    return true;
            }

            return false;
        }

        public static bool IntersectsWith(OrientedBox shape, MathSphere sphere) =>
            Distance.Squared(sphere.Center, sphere.Center, shape) <= sphere.Radius * sphere.Radius;

        private static bool IntersectsWithProjectedRectangle(OrientedBox shape,
            Vector2D<float> minimum,
            Vector2D<float> maximum
        )
        {
            var corners = GetWorldCorners(shape);
            var projectedCorners = corners
                .Select(corner => new Vector2D<float>(corner.X, corner.Y))
                .ToArray();
            var boxCorners = new[]
            {
            minimum,
            new Vector2D<float>(maximum.X, minimum.Y),
            maximum,
            new Vector2D<float>(minimum.X, maximum.Y),
        };
            var edges = GetWorldEdges(shape);
            var axes = new List<Vector2D<float>>
        {
            Vector2D<float>.UnitX,
            Vector2D<float>.UnitY,
        };

            foreach (var edge in edges)
            {
                var projectedEdge = new Vector2D<float>(edge.X, edge.Y);
                axes.Add(new Vector2D<float>(-projectedEdge.Y, projectedEdge.X));
            }

            return !HasSeparatingAxis(projectedCorners, boxCorners, axes);
        }

        private static List<Vector2D<float>> GetConvexHull(IEnumerable<Vector2D<float>> points)
        {
            var sorted = points.OrderBy(point => point.X).ThenBy(point => point.Y).ToArray();
            if (sorted.Length <= 1)
                return [.. sorted];

            var hull = new List<Vector2D<float>>();
            foreach (var point in sorted)
            {
                while (
                    hull.Count >= 2
                    && Cross(hull[^1] - hull[^2], point - hull[^1]) <= 0f
                )
                    hull.RemoveAt(hull.Count - 1);

                hull.Add(point);
            }

            var lowerHullCount = hull.Count;
            for (var index = sorted.Length - 2; index >= 0; index--)
            {
                var point = sorted[index];
                while (
                    hull.Count > lowerHullCount
                    && Cross(hull[^1] - hull[^2], point - hull[^1]) <= 0f
                )
                    hull.RemoveAt(hull.Count - 1);

                hull.Add(point);
            }

            hull.RemoveAt(hull.Count - 1);
            return hull;
        }

        private static float Cross(Vector2D<float> first, Vector2D<float> second) =>
            first.X * second.Y - first.Y * second.X;

        public static bool IntersectsWith(OrientedBox shape, Box3D<float> box)
        {
            var boxCorners = GetBoxCorners(box);
            return IntersectsParallelepipeds(
                GetWorldCorners(shape),
                GetWorldEdges(shape),
                boxCorners,
                GetBoxEdges(box)
            );
        }

        internal static Vector3D<float>[] GetWorldCorners(OrientedBox shape)
        {
            var transform = shape.Transform;
            var corners = new Vector3D<float>[8];
            for (var index = 0; index < corners.Length; index++)
            {
                var local = new Vector3D<float>(
                    (index & 1) == 0 ? shape.Minimum.X : shape.Maximum.X,
                    (index & 2) == 0 ? shape.Minimum.Y : shape.Maximum.Y,
                    (index & 4) == 0 ? shape.Minimum.Z : shape.Maximum.Z
                );
                corners[index] = TransformPoint(local, transform);
            }

            return corners;
        }

        private static Vector3D<float>[] GetWorldEdges(OrientedBox shape)
        {
            var transform = shape.Transform;
            return
            [
                TransformDirection(
                new Vector3D<float>(shape.Maximum.X - shape.Minimum.X, 0f, 0f),
                transform
            ),
            TransformDirection(
                new Vector3D<float>(0f, shape.Maximum.Y - shape.Minimum.Y, 0f),
                transform
            ),
            TransformDirection(
                new Vector3D<float>(0f, 0f, shape.Maximum.Z - shape.Minimum.Z),
                transform
            ),
        ];
        }

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

        private static Vector3D<float> TransformDirection(
            Vector3D<float> direction,
            Matrix4X4<float> transform
        )
        {
            var transformed = Vector4D.Transform(new Vector4D<float>(direction, 0f), transform);
            return new Vector3D<float>(transformed.X, transformed.Y, transformed.Z);
        }

        private static Vector3D<float>[] GetBoxCorners(Box3D<float> box) =>
        [
            new(box.Min.X, box.Min.Y, box.Min.Z),
        new(box.Max.X, box.Min.Y, box.Min.Z),
        new(box.Min.X, box.Max.Y, box.Min.Z),
        new(box.Max.X, box.Max.Y, box.Min.Z),
        new(box.Min.X, box.Min.Y, box.Max.Z),
        new(box.Max.X, box.Min.Y, box.Max.Z),
        new(box.Min.X, box.Max.Y, box.Max.Z),
        new(box.Max.X, box.Max.Y, box.Max.Z),
    ];

        private static Vector3D<float>[] GetBoxEdges(Box3D<float> box) =>
        [
            new Vector3D<float>(box.Max.X - box.Min.X, 0f, 0f),
        new Vector3D<float>(0f, box.Max.Y - box.Min.Y, 0f),
        new Vector3D<float>(0f, 0f, box.Max.Z - box.Min.Z),
    ];

        private static bool IntersectsParallelepipeds(
            Vector3D<float>[] firstCorners,
            Vector3D<float>[] firstEdges,
            Vector3D<float>[] secondCorners,
            Vector3D<float>[] secondEdges
        )
        {
            var axes = new List<Vector3D<float>>
        {
            Vector3D.Cross(firstEdges[1], firstEdges[2]),
            Vector3D.Cross(firstEdges[2], firstEdges[0]),
            Vector3D.Cross(firstEdges[0], firstEdges[1]),
            Vector3D.Cross(secondEdges[1], secondEdges[2]),
            Vector3D.Cross(secondEdges[2], secondEdges[0]),
            Vector3D.Cross(secondEdges[0], secondEdges[1]),
        };

            foreach (var firstEdge in firstEdges)
                foreach (var secondEdge in secondEdges)
                    axes.Add(Vector3D.Cross(firstEdge, secondEdge));

            // Flat boxes need in-plane edge normals. Point and line boxes also need
            // directions perpendicular to their affine span, which face normals omit.
            var faceNormals = axes.Take(6).ToArray();
            var offset = secondCorners[0] - firstCorners[0];
            axes.Add(offset);
            foreach (var edge in firstEdges.Concat(secondEdges))
            {
                axes.Add(Vector3D.Cross(edge, Vector3D.Cross(offset, edge)));
                foreach (var normal in faceNormals)
                    axes.Add(Vector3D.Cross(normal, edge));
            }

            return !HasSeparatingAxis(firstCorners, secondCorners, axes);
        }

        private static bool HasSeparatingAxis(
            IReadOnlyList<Vector3D<float>> first,
            IReadOnlyList<Vector3D<float>> second,
            IReadOnlyList<Vector3D<float>> axes
        )
        {
            foreach (var axis in axes)
            {
                if (axis.LengthSquared == 0f)
                    continue;

                Project(first, axis, out var firstMinimum, out var firstMaximum);
                Project(second, axis, out var secondMinimum, out var secondMaximum);
                if (firstMaximum < secondMinimum || secondMaximum < firstMinimum)
                    return true;
            }

            return false;
        }

        private static bool HasSeparatingAxis(
            IReadOnlyList<Vector2D<float>> first,
            IReadOnlyList<Vector2D<float>> second,
            IReadOnlyList<Vector2D<float>> axes
        )
        {
            foreach (var axis in axes)
            {
                if (axis.LengthSquared == 0f)
                    continue;

                Project(first, axis, out var firstMinimum, out var firstMaximum);
                Project(second, axis, out var secondMinimum, out var secondMaximum);
                if (firstMaximum < secondMinimum || secondMaximum < firstMinimum)
                    return true;
            }

            return false;
        }

        private static void Project(
            IReadOnlyList<Vector3D<float>> points,
            Vector3D<float> axis,
            out float minimum,
            out float maximum
        )
        {
            minimum = maximum = Vector3D.Dot(points[0], axis);
            for (var index = 1; index < points.Count; index++)
            {
                var projection = Vector3D.Dot(points[index], axis);
                minimum = MathF.Min(minimum, projection);
                maximum = MathF.Max(maximum, projection);
            }
        }

        private static void Project(
            IReadOnlyList<Vector2D<float>> points,
            Vector2D<float> axis,
            out float minimum,
            out float maximum
        )
        {
            minimum = maximum = Vector2D.Dot(points[0], axis);
            for (var index = 1; index < points.Count; index++)
            {
                var projection = Vector2D.Dot(points[index], axis);
                minimum = MathF.Min(minimum, projection);
                maximum = MathF.Max(maximum, projection);
            }
        }

        public static bool IntersectsWith(OrientedBox shape, OrientedBox other) =>
            IntersectsParallelepipeds(GetWorldCorners(shape), GetWorldEdges(shape), GetWorldCorners(other), GetWorldEdges(other));

        public static Box3D<float> GetBounds(OrientedBox shape)
        {
            var corners = GetWorldCorners(shape);
            var minimum = corners[0];
            var maximum = minimum;
            foreach (var corner in corners)
            {
                minimum = new(
                    MathF.Min(minimum.X, corner.X),
                    MathF.Min(minimum.Y, corner.Y),
                    MathF.Min(minimum.Z, corner.Z)
                );
                maximum = new(
                    MathF.Max(maximum.X, corner.X),
                    MathF.Max(maximum.Y, corner.Y),
                    MathF.Max(maximum.Z, corner.Z)
                );
            }
            return new(minimum, maximum);
        }

    }
}
