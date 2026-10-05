namespace Nexus.Physics;

using Silk.NET.Maths;

/// <summary>Identifies the geometry represented by a 2D physics shape.</summary>
public enum PhysicsShapeKind
{
    /// <summary>A circular shape.</summary>
    Circle,
    /// <summary>An axis-aligned rectangle in local space.</summary>
    Rectangle,
    /// <summary>A capsule defined by a segment and radius.</summary>
    Capsule,
    /// <summary>A convex polygon.</summary>
    ConvexPolygon,
    /// <summary>A triangulated mesh.</summary>
    TriangleMesh,
}

/// <summary>Describes the local-space bounds of a 2D physics shape.</summary>
public readonly record struct PhysicsBounds2D(
    Vector2D<float> Minimum,
    Vector2D<float> Maximum
)
{
    /// <summary>Gets whether the bounds contain only finite coordinates.</summary>
    public bool IsFinite =>
        float.IsFinite(Minimum.X) && float.IsFinite(Minimum.Y)
        && float.IsFinite(Maximum.X) && float.IsFinite(Maximum.Y);

    /// <summary>Gets whether the bounds are finite and have a non-inverted range.</summary>
    public bool IsValid =>
        IsFinite
        && Minimum.X <= Maximum.X
        && Minimum.Y <= Maximum.Y;
}

/// <summary>Provides local-space geometry information for a 2D collider.</summary>
public interface IPhysicsShape2D
{
    /// <summary>Gets the concrete geometry kind.</summary>
    PhysicsShapeKind Kind { get; }

    /// <summary>Gets the axis-aligned local-space bounds.</summary>
    PhysicsBounds2D LocalBounds { get; }
}

/// <summary>Represents a circle in local space.</summary>
public sealed class CircleShape2D : IPhysicsShape2D
{
    /// <summary>Creates a circle shape.</summary>
    /// <param name="radius">The circle radius.</param>
    /// <param name="center">The local-space center.</param>
    public CircleShape2D(float radius, Vector2D<float> center = default)
    {
        if (radius <= 0f || !float.IsFinite(radius))
            throw new ArgumentOutOfRangeException(nameof(radius));

        Radius = radius;
        Center = center;
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Y))
            throw new ArgumentOutOfRangeException(nameof(center));
    }

    /// <summary>Gets the local-space center.</summary>
    public Vector2D<float> Center { get; }

    /// <summary>Gets the radius.</summary>
    public float Radius { get; }

    /// <inheritdoc />
    public PhysicsShapeKind Kind => PhysicsShapeKind.Circle;

    /// <inheritdoc />
    public PhysicsBounds2D LocalBounds =>
        new(Center - new Vector2D<float>(Radius), Center + new Vector2D<float>(Radius));
}

/// <summary>Represents a local-space rectangle.</summary>
public sealed class RectangleShape2D : IPhysicsShape2D
{
    /// <summary>Creates a rectangle shape.</summary>
    /// <param name="size">The positive local-space width and height.</param>
    /// <param name="center">The local-space center.</param>
    public RectangleShape2D(Vector2D<float> size, Vector2D<float> center = default)
    {
        if (size.X <= 0f || size.Y <= 0f || !float.IsFinite(size.X) || !float.IsFinite(size.Y))
            throw new ArgumentOutOfRangeException(nameof(size));

        Size = size;
        Center = center;
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Y))
            throw new ArgumentOutOfRangeException(nameof(center));
    }

    /// <summary>Gets the local-space center.</summary>
    public Vector2D<float> Center { get; }

    /// <summary>Gets the local-space size.</summary>
    public Vector2D<float> Size { get; }

    /// <inheritdoc />
    public PhysicsShapeKind Kind => PhysicsShapeKind.Rectangle;

    /// <inheritdoc />
    public PhysicsBounds2D LocalBounds
    {
        get
        {
            var halfSize = Size / 2f;
            return new(Center - halfSize, Center + halfSize);
        }
    }
}

/// <summary>Represents a capsule around a local-space line segment.</summary>
public sealed class CapsuleShape2D : IPhysicsShape2D
{
    /// <summary>Creates a capsule shape.</summary>
    /// <param name="first">The first local-space segment endpoint.</param>
    /// <param name="second">The second local-space segment endpoint.</param>
    /// <param name="radius">The capsule radius.</param>
    public CapsuleShape2D(Vector2D<float> first, Vector2D<float> second, float radius)
    {
        if (radius <= 0f || !float.IsFinite(radius))
            throw new ArgumentOutOfRangeException(nameof(radius));

        First = first;
        Second = second;
        Radius = radius;
        if (!Finite(first) || !Finite(second))
            throw new ArgumentOutOfRangeException(nameof(first));
        if (first == second)
            throw new ArgumentException("Capsule endpoints must be distinct.", nameof(second));
    }

    /// <summary>Gets the first segment endpoint.</summary>
    public Vector2D<float> First { get; }

    /// <summary>Gets the second segment endpoint.</summary>
    public Vector2D<float> Second { get; }

    /// <summary>Gets the capsule radius.</summary>
    public float Radius { get; }

    /// <inheritdoc />
    public PhysicsShapeKind Kind => PhysicsShapeKind.Capsule;

    /// <inheritdoc />
    public PhysicsBounds2D LocalBounds =>
        new(
            new(MathF.Min(First.X, Second.X) - Radius, MathF.Min(First.Y, Second.Y) - Radius),
            new(MathF.Max(First.X, Second.X) + Radius, MathF.Max(First.Y, Second.Y) + Radius)
        );

    private static bool Finite(Vector2D<float> value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y);
}

/// <summary>Represents a convex polygon in local space.</summary>
public sealed class ConvexPolygonShape2D : IPhysicsShape2D
{
    /// <summary>Creates a convex polygon from local-space vertices.</summary>
    /// <param name="vertices">The polygon vertices in winding order.</param>
    public ConvexPolygonShape2D(IEnumerable<Vector2D<float>> vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        Vertices = Array.AsReadOnly(vertices.ToArray());
        if (Vertices.Count < 3)
            throw new ArgumentException("At least three vertices are required.", nameof(vertices));
        if (Vertices.Any(vertex => !Finite(vertex)))
            throw new ArgumentException("Polygon vertices must be finite.", nameof(vertices));

        LocalBounds = new(
            new(Vertices.Min(vertex => vertex.X), Vertices.Min(vertex => vertex.Y)),
            new(Vertices.Max(vertex => vertex.X), Vertices.Max(vertex => vertex.Y))
        );
        var winding = Cross(Vertices[1] - Vertices[0], Vertices[2] - Vertices[1]);
        if (winding == 0f)
            throw new ArgumentException("Polygon edges must be nondegenerate.", nameof(vertices));
        for (var index = 0; index < Vertices.Count; index++)
        {
            var current = Vertices[index];
            var next = Vertices[(index + 1) % Vertices.Count];
            var following = Vertices[(index + 2) % Vertices.Count];
            if (Cross(next - current, following - next) == 0f)
                throw new ArgumentException("Polygon edges must be nondegenerate.", nameof(vertices));
            for (var other = index + 1; other < Vertices.Count; other++)
                if (other != (index + 1) % Vertices.Count
                    && (other + 1) % Vertices.Count != index
                    && EdgesIntersect(current, next, Vertices[other], Vertices[(other + 1) % Vertices.Count]))
                    throw new ArgumentException("Polygon boundaries must not self-intersect.", nameof(vertices));
        }
        for (var index = 0; index < Vertices.Count; index++)
        {
            var turn = Cross(
                Vertices[(index + 1) % Vertices.Count] - Vertices[index],
                Vertices[(index + 2) % Vertices.Count] - Vertices[(index + 1) % Vertices.Count]
            );
            if ((turn > 0f) != (winding > 0f))
                throw new ArgumentException("Polygon vertices must describe a convex boundary.", nameof(vertices));
        }
    }

    /// <summary>Gets the polygon vertices in winding order.</summary>
    public IReadOnlyList<Vector2D<float>> Vertices { get; }

    /// <inheritdoc />
    public PhysicsShapeKind Kind => PhysicsShapeKind.ConvexPolygon;

    /// <inheritdoc />
    public PhysicsBounds2D LocalBounds { get; }

    private static bool Finite(Vector2D<float> value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y);

    private static float Cross(Vector2D<float> first, Vector2D<float> second) =>
        first.X * second.Y - first.Y * second.X;

    private static bool EdgesIntersect(
        Vector2D<float> first,
        Vector2D<float> second,
        Vector2D<float> third,
        Vector2D<float> fourth
    ) =>
        PhysicsTriangle2D.SegmentsIntersect(first, second, third, fourth);
}

/// <summary>Represents a triangulated concave or convex mesh in local space.</summary>
public sealed class TriangleMeshShape2D : IPhysicsShape2D
{
    /// <summary>Creates a triangle mesh shape.</summary>
    /// <param name="triangles">The local-space triangles.</param>
    public TriangleMeshShape2D(IEnumerable<PhysicsTriangle2D> triangles)
    {
        ArgumentNullException.ThrowIfNull(triangles);
        Triangles = Array.AsReadOnly(triangles.ToArray());
        if (Triangles.Count == 0)
            throw new ArgumentException("At least one triangle is required.", nameof(triangles));
        if (Triangles.Any(triangle => !triangle.IsValid))
            throw new ArgumentException("Triangles must be finite and nondegenerate.", nameof(triangles));

        var points = Triangles
            .SelectMany(triangle => new[] { triangle.First, triangle.Second, triangle.Third })
            .ToArray();
        LocalBounds = new(
            new(points.Min(point => point.X), points.Min(point => point.Y)),
            new(points.Max(point => point.X), points.Max(point => point.Y))
        );
    }

    /// <summary>Gets the local-space triangles.</summary>
    public IReadOnlyList<PhysicsTriangle2D> Triangles { get; }

    /// <inheritdoc />
    public PhysicsShapeKind Kind => PhysicsShapeKind.TriangleMesh;

    /// <inheritdoc />
    public PhysicsBounds2D LocalBounds { get; }
}

/// <summary>Represents one local-space triangle used by a 2D collider.</summary>
public readonly record struct PhysicsTriangle2D(
    Vector2D<float> First,
    Vector2D<float> Second,
    Vector2D<float> Third
)
{
    internal bool IsValid =>
        Finite(First) && Finite(Second) && Finite(Third)
        && Cross(Second - First, Third - First) != 0f;

    /// <summary>Determines whether this triangle overlaps another triangle.</summary>
    /// <param name="other">The other triangle.</param>
    /// <returns><see langword="true"/> when the triangles intersect or contain one another.</returns>
    public bool Overlaps(PhysicsTriangle2D other)
    {
        if (SegmentsIntersect(First, Second, other.First, other.Second)
            || SegmentsIntersect(First, Second, other.Second, other.Third)
            || SegmentsIntersect(First, Second, other.Third, other.First)
            || SegmentsIntersect(Second, Third, other.First, other.Second)
            || SegmentsIntersect(Second, Third, other.Second, other.Third)
            || SegmentsIntersect(Second, Third, other.Third, other.First)
            || SegmentsIntersect(Third, First, other.First, other.Second)
            || SegmentsIntersect(Third, First, other.Second, other.Third)
            || SegmentsIntersect(Third, First, other.Third, other.First))
            return true;
        return Contains(First, other) || other.Contains(other.First, this);
    }

    private bool Contains(Vector2D<float> point, PhysicsTriangle2D triangle) =>
        SameSide(point, triangle.First, triangle.Second, triangle.Third)
        && SameSide(point, triangle.Second, triangle.Third, triangle.First)
        && SameSide(point, triangle.Third, triangle.First, triangle.Second);

    private static bool SameSide(
        Vector2D<float> point,
        Vector2D<float> first,
        Vector2D<float> second,
        Vector2D<float> opposite
    )
    {
        var pointTurn = Cross(second - first, point - first);
        var oppositeTurn = Cross(second - first, opposite - first);
        return pointTurn == 0f
            || oppositeTurn == 0f
            || (pointTurn > 0f) == (oppositeTurn > 0f);
    }

    internal static bool SegmentsIntersect(Vector2D<float> first, Vector2D<float> second, Vector2D<float> third, Vector2D<float> fourth)
    {
        var firstTurn = Cross(second - first, third - first);
        var secondTurn = Cross(second - first, fourth - first);
        var thirdTurn = Cross(fourth - third, first - third);
        var fourthTurn = Cross(fourth - third, second - third);
        return ((firstTurn < 0f) != (secondTurn < 0f)
                && (thirdTurn < 0f) != (fourthTurn < 0f)
                && firstTurn != 0f
                && secondTurn != 0f
                && thirdTurn != 0f
                && fourthTurn != 0f)
            || (firstTurn == 0f && OnSegment(first, second, third))
            || (secondTurn == 0f && OnSegment(first, second, fourth))
            || (thirdTurn == 0f && OnSegment(third, fourth, first))
            || (fourthTurn == 0f && OnSegment(third, fourth, second));
    }

    private static bool OnSegment(Vector2D<float> first, Vector2D<float> second, Vector2D<float> point) =>
        point.X >= MathF.Min(first.X, second.X) && point.X <= MathF.Max(first.X, second.X)
        && point.Y >= MathF.Min(first.Y, second.Y) && point.Y <= MathF.Max(first.Y, second.Y);

    private static bool Finite(Vector2D<float> value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y);

    private static float Cross(Vector2D<float> left, Vector2D<float> right) =>
        left.X * right.Y - left.Y * right.X;

}
