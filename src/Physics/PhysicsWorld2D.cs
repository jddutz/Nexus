namespace Nexus.Physics;

using Nexus.Physics.Components;
using Silk.NET.Maths;

/// <summary>Owns the simulation state for one isolated 2D physics space.</summary>
public sealed class PhysicsWorld2D
{
    private readonly Dictionary<PhysicsBody2D, IGameObject2D> _bodies = [];
    private readonly HashSet<PhysicsCollider2D> _colliders = [];
    private readonly Dictionary<PhysicsCollider2D, ColliderGeometry> _geometry = [];

    /// <summary>Creates a world with a new identity.</summary>
    public PhysicsWorld2D() : this(PhysicsWorldId.New()) { }

    /// <summary>Creates a world with the specified identity.</summary>
    /// <param name="id">The world identity.</param>
    internal PhysicsWorld2D(PhysicsWorldId id) => Id = id;

    /// <summary>Gets the identity of this simulation world.</summary>
    public PhysicsWorldId Id { get; }

    /// <summary>Adds a body to this simulation space.</summary>
    /// <param name="body">The body to add.</param>
    internal void Add(PhysicsBody2D body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (body.Owner is not IGameObject2D owner)
            throw new InvalidOperationException("A physics body requires an IGameObject2D owner.");
        if (!PhysicsPose.IsSupportedParent(owner))
            throw new InvalidOperationException("Physics bodies require rigid, invertible parent transforms.");
        body.InitializePose();
        _bodies[body] = owner;
    }

    /// <summary>Adds a collider to this simulation space.</summary>
    /// <param name="collider">The collider to add.</param>
    internal void Add(PhysicsCollider2D collider)
    {
        ArgumentNullException.ThrowIfNull(collider);
        if (collider.Owner is not IGameObject2D)
            throw new InvalidOperationException("A physics collider requires an IGameObject2D owner.");
        var localTriangles = PhysicsGeometry.Triangulate(collider.Shape).ToArray();
        var geometry = new ColliderGeometry(localTriangles);
        UpdateWorldGeometry(geometry, ((IGameObject2D)collider.Owner).WorldTransform);
        geometry.PreviousWorldBounds = geometry.WorldBounds;
        geometry.HasPreviousWorldBounds = true;
        _colliders.Add(collider);
        _geometry[collider] = geometry;
        collider.ShapeChanged += OnColliderShapeChanged;
    }

    /// <summary>Removes a body from this simulation space.</summary>
    /// <param name="body">The body to remove.</param>
    internal void Remove(PhysicsBody2D body) => _bodies.Remove(body);

    /// <summary>Removes a collider from this simulation space.</summary>
    /// <param name="collider">The collider to remove.</param>
    internal void Remove(PhysicsCollider2D collider)
    {
        if (!_colliders.Remove(collider))
            return;
        collider.ShapeChanged -= OnColliderShapeChanged;
        _geometry.Remove(collider);
    }

    /// <summary>Gets whether this world has registered participants.</summary>
    internal bool HasParticipants => _bodies.Count != 0 || _colliders.Count != 0;

    /// <summary>Advances bodies and resolves static-target rectangle sweeps before overlap reporting.</summary>
    /// <param name="deltaTime">The elapsed simulation time in seconds.</param>
    /// <returns>Ordinary overlaps and the earliest swept contact for each moving body.</returns>
    internal IReadOnlyList<CollisionResult> Step(float deltaTime)
    {
        var bodies = _bodies.Keys
            .OrderBy(body => PhysicsPose.Depth(body.Owner))
            .ThenBy(body => body.Owner?.Id.Value ?? 0)
            .ToArray();
        var previousPositions = bodies.ToDictionary(body => body, body => body.WorldPosition);
        var previousRotations = bodies.ToDictionary(body => body, body => body.WorldRotation);
        foreach (var body in bodies)
            body.IntegrateWorld(deltaTime);
        foreach (var body in bodies)
            body.WriteBackPose();

        var colliders = _colliders
            .Where(collider => collider.Owner is IGameObject2D)
            .Select(collider => (Collider: collider, Owner: (IGameObject2D)collider.Owner!))
            .ToArray();
        var geometry = colliders
            .Select(entry =>
            {
                var cached = _geometry[entry.Collider];
                UpdateWorldGeometry(cached, entry.Owner.WorldTransform);
                return (entry.Collider, entry.Owner, Geometry: cached);
            })
            .ToArray();
        var sweepCandidates = new List<(
            int First,
            int Second,
            PhysicsBody2D Body,
            PhysicsCollider2D MovingCollider,
            PhysicsCollider2D TargetCollider,
            PhysicsCollisionContact Contact
        )>();
        for (var first = 0; first < geometry.Length; first++)
        for (var second = first + 1; second < geometry.Length; second++)
        {
            if (ReferenceEquals(geometry[first].Owner, geometry[second].Owner)
                || !CanCollide(geometry[first].Collider, geometry[second].Collider))
                continue;
            if (TryCreateSweepContact(
                geometry[first].Collider,
                geometry[first].Owner,
                geometry[first].Geometry,
                geometry[second].Collider,
                geometry[second].Owner,
                geometry[second].Geometry,
                previousPositions,
                previousRotations,
                deltaTime,
                out var firstBody,
                out var firstContact
            ))
            {
                sweepCandidates.Add((
                    first,
                    second,
                    firstBody!,
                    geometry[first].Collider,
                    geometry[second].Collider,
                    firstContact
                ));
            }
            else if (TryCreateSweepContact(
                geometry[second].Collider,
                geometry[second].Owner,
                geometry[second].Geometry,
                geometry[first].Collider,
                geometry[first].Owner,
                geometry[first].Geometry,
                previousPositions,
                previousRotations,
                deltaTime,
                out var secondBody,
                out var secondContact
            ))
            {
                sweepCandidates.Add((
                    first,
                    second,
                    secondBody!,
                    geometry[second].Collider,
                    geometry[first].Collider,
                    secondContact
                ));
            }
        }

        var selectedContacts = new Dictionary<(int First, int Second), PhysicsCollisionContact>();
        var sweepPairs = sweepCandidates
            .Select(candidate => (candidate.First, candidate.Second))
            .ToHashSet();
        foreach (var body in bodies)
        {
            var candidates = sweepCandidates
                .Where(candidate => ReferenceEquals(candidate.Body, body))
                .ToArray();
            if (candidates.Length == 0)
                continue;

            var selected = candidates
                .OrderBy(candidate => candidate.Contact.Fraction)
                .ThenBy(candidate => candidate.TargetCollider.Id.Value)
                .ThenBy(candidate => candidate.MovingCollider.Id.Value)
                .First();
            var previousPosition = previousPositions[body];
            var position = previousPosition
                + (body.WorldPosition - previousPosition) * selected.Contact.Fraction;
            body.StopAtContact(position);
            selectedContacts[(selected.First, selected.Second)] = selected.Contact;
        }

        foreach (var body in bodies)
            body.WriteBackPose();
        foreach (var entry in geometry)
            UpdateWorldGeometry(entry.Geometry, entry.Owner.WorldTransform);

        var overlaps = new List<CollisionResult>();
        for (var first = 0; first < geometry.Length; first++)
        for (var second = first + 1; second < geometry.Length; second++)
        {
            if (ReferenceEquals(geometry[first].Owner, geometry[second].Owner)
                || !CanCollide(geometry[first].Collider, geometry[second].Collider))
                continue;

            var pair = (first, second);
            if (selectedContacts.TryGetValue(pair, out var contact))
            {
                overlaps.Add(new(geometry[first].Collider, geometry[second].Collider, contact));
                continue;
            }
            if (sweepPairs.Contains(pair))
                continue;
            if (Overlaps(geometry[first].Geometry, geometry[second].Geometry))
                overlaps.Add(new(geometry[first].Collider, geometry[second].Collider, null));
        }

        foreach (var body in bodies)
            body.WriteBackPose();
        foreach (var entry in geometry)
        {
            entry.Geometry.PreviousWorldBounds = entry.Geometry.WorldBounds;
            entry.Geometry.HasPreviousWorldBounds = true;
        }
        return overlaps;
    }

    /// <summary>Checks whether two colliders mutually include each other's category masks.</summary>
    /// <param name="first">The first collider.</param>
    /// <param name="second">The second collider.</param>
    /// <returns><see langword="true"/> when the collider pair is enabled.</returns>
    private static bool CanCollide(PhysicsCollider2D first, PhysicsCollider2D second) =>
        (first.CollisionCategory & second.CollisionMask) != 0
        && (second.CollisionCategory & first.CollisionMask) != 0;

    /// <summary>
    /// Creates a sweep candidate for a translating, axis-aligned rectangle against an unmoved static rectangle.
    /// </summary>
    /// <param name="movingCollider">The collider whose body may move during this step.</param>
    /// <param name="movingOwner">The moving collider's owner.</param>
    /// <param name="movingGeometry">The moving collider's transformed geometry.</param>
    /// <param name="targetCollider">The potential static target collider.</param>
    /// <param name="targetOwner">The target collider's owner.</param>
    /// <param name="targetGeometry">The target collider's transformed geometry.</param>
    /// <param name="previousPositions">The bodies' world positions before integration.</param>
    /// <param name="previousRotations">The bodies' world rotations before integration.</param>
    /// <param name="deltaTime">The elapsed simulation time in seconds.</param>
    /// <param name="body">The moving body when a candidate is found.</param>
    /// <param name="contact">The candidate contact data.</param>
    /// <returns><see langword="true"/> when a valid entering sweep is found.</returns>
    private bool TryCreateSweepContact(
        PhysicsCollider2D movingCollider,
        IGameObject2D movingOwner,
        ColliderGeometry movingGeometry,
        PhysicsCollider2D targetCollider,
        IGameObject2D targetOwner,
        ColliderGeometry targetGeometry,
        IReadOnlyDictionary<PhysicsBody2D, Vector2D<float>> previousPositions,
        IReadOnlyDictionary<PhysicsBody2D, float> previousRotations,
        float deltaTime,
        out PhysicsBody2D? body,
        out PhysicsCollisionContact contact
    )
    {
        body = _bodies.FirstOrDefault(entry => ReferenceEquals(entry.Value, movingOwner)).Key;
        contact = default;
        if (
            body is null
            || _bodies.Any(entry => ReferenceEquals(entry.Value, targetOwner))
            || deltaTime <= 0f
            || movingCollider.Shape is not RectangleShape2D
            || targetCollider.Shape is not RectangleShape2D
            || !IsAxisAligned(movingOwner.WorldTransform)
            || !IsAxisAligned(targetOwner.WorldTransform)
            || !targetGeometry.HasPreviousWorldBounds
            || targetGeometry.PreviousWorldBounds != targetGeometry.WorldBounds
        )
            return false;

        var previousPosition = previousPositions[body];
        if (
            MathF.Abs(previousRotations[body] - body.WorldRotation) > 0.0001f
            || body.WorldPosition == previousPosition
        )
            return false;

        var startOffset = previousPosition - body.WorldPosition;
        var startBounds = new PhysicsBounds2D(
            movingGeometry.WorldBounds.Minimum + startOffset,
            movingGeometry.WorldBounds.Maximum + startOffset
        );
        if (HasPositiveOverlap(startBounds, targetGeometry.WorldBounds))
            return false;

        var startCenter = (startBounds.Minimum + startBounds.Maximum) / 2f;
        var endCenter = (movingGeometry.WorldBounds.Minimum + movingGeometry.WorldBounds.Maximum) / 2f;
        var halfSize = (movingGeometry.WorldBounds.Maximum - movingGeometry.WorldBounds.Minimum) / 2f;
        var expanded = new PhysicsBounds2D(
            targetGeometry.WorldBounds.Minimum - halfSize,
            targetGeometry.WorldBounds.Maximum + halfSize
        );
        if (!SweepPoint(startCenter, endCenter, expanded, out var fraction, out var normal))
            return false;

        contact = new(body, normal, fraction, body.Velocity);
        return true;
    }

    /// <summary>Determines whether a world transform keeps rectangle axes axis-aligned.</summary>
    /// <param name="transform">The world transform to inspect.</param>
    /// <returns><see langword="true"/> when the transform has no arbitrary rotation or shear.</returns>
    private static bool IsAxisAligned(Matrix4X4<float> transform)
    {
        const float tolerance = 0.0001f;
        return (MathF.Abs(transform.M12) < tolerance && MathF.Abs(transform.M21) < tolerance)
            || (MathF.Abs(transform.M11) < tolerance && MathF.Abs(transform.M22) < tolerance);
    }

    /// <summary>Determines whether two bounds overlap with positive area.</summary>
    /// <param name="first">The first bounds.</param>
    /// <param name="second">The second bounds.</param>
    /// <returns><see langword="true"/> when the bounds penetrate rather than merely touch.</returns>
    private static bool HasPositiveOverlap(PhysicsBounds2D first, PhysicsBounds2D second) =>
        first.Minimum.X < second.Maximum.X
        && first.Maximum.X > second.Minimum.X
        && first.Minimum.Y < second.Maximum.Y
        && first.Maximum.Y > second.Minimum.Y;

    /// <summary>Finds the first valid entering contact of a point and axis-aligned bounds.</summary>
    /// <param name="start">The initial point.</param>
    /// <param name="end">The final point.</param>
    /// <param name="bounds">The bounds expanded by the moving rectangle's half-size.</param>
    /// <param name="fraction">The normalized step fraction of contact.</param>
    /// <param name="normal">The nonzero outward normal at contact.</param>
    /// <returns><see langword="true"/> when the point enters the bounds during the step.</returns>
    private static bool SweepPoint(
        Vector2D<float> start,
        Vector2D<float> end,
        PhysicsBounds2D bounds,
        out float fraction,
        out Vector2D<float> normal
    )
    {
        fraction = 0f;
        normal = default;
        var delta = end - start;
        if (delta == default)
            return false;

        var entry = float.NegativeInfinity;
        var exit = float.PositiveInfinity;
        var entryNormal = default(Vector2D<float>);
        if (!SweepAxis(start.X, delta.X, bounds.Minimum.X, bounds.Maximum.X, new(-1f, 0f), new(1f, 0f), ref entry, ref exit, ref entryNormal)
            || !SweepAxis(start.Y, delta.Y, bounds.Minimum.Y, bounds.Maximum.Y, new(0f, -1f), new(0f, 1f), ref entry, ref exit, ref entryNormal))
            return false;
        if (entry < 0f || entry > 1f || exit < 0f || entry > exit || entryNormal == default)
            return false;

        fraction = entry;
        normal = entryNormal;
        return true;
    }

    /// <summary>Clips a point sweep against one axis-aligned interval.</summary>
    /// <param name="start">The initial axis coordinate.</param>
    /// <param name="delta">The displacement along this axis.</param>
    /// <param name="minimum">The interval's lower bound.</param>
    /// <param name="maximum">The interval's upper bound.</param>
    /// <param name="minimumNormal">The outward normal at the lower bound.</param>
    /// <param name="maximumNormal">The outward normal at the upper bound.</param>
    /// <param name="entry">The greatest entry fraction accumulated so far.</param>
    /// <param name="exit">The least exit fraction accumulated so far.</param>
    /// <param name="entryNormal">The normal associated with the current entry.</param>
    /// <returns><see langword="true"/> when the axis intervals overlap in time.</returns>
    private static bool SweepAxis(
        float start,
        float delta,
        float minimum,
        float maximum,
        Vector2D<float> minimumNormal,
        Vector2D<float> maximumNormal,
        ref float entry,
        ref float exit,
        ref Vector2D<float> entryNormal
    )
    {
        if (delta == 0f)
            return start >= minimum && start <= maximum;

        var first = (minimum - start) / delta;
        var second = (maximum - start) / delta;
        var axisEntry = MathF.Min(first, second);
        var axisExit = MathF.Max(first, second);
        if (axisEntry > entry)
            entryNormal = first < second ? minimumNormal : maximumNormal;
        entry = MathF.Max(entry, axisEntry);
        exit = MathF.Min(exit, axisExit);
        return entry <= exit;
    }

    /// <summary>Rebuilds a collider's cached local geometry after its shape changes.</summary>
    /// <param name="collider">The collider whose geometry changed.</param>
    /// <param name="previous">The previous shape.</param>
    /// <param name="current">The replacement shape.</param>
    private void OnColliderShapeChanged(
        PhysicsCollider2D collider,
        IPhysicsShape2D previous,
        IPhysicsShape2D current
    )
    {
        if (_colliders.Contains(collider))
        {
            var localTriangles = PhysicsGeometry.Triangulate(current).ToArray();
            _geometry[collider] = new ColliderGeometry(localTriangles);
        }
    }

    /// <summary>Transforms cached local triangles and bounds into world space.</summary>
    /// <param name="geometry">The cached collider geometry.</param>
    /// <param name="transform">The owner's world transform.</param>
    private static void UpdateWorldGeometry(ColliderGeometry geometry, Matrix4X4<float> transform)
    {
        var minimum = new Vector2D<float>(float.PositiveInfinity);
        var maximum = new Vector2D<float>(float.NegativeInfinity);
        for (var index = 0; index < geometry.LocalTriangles.Length; index++)
        {
            var local = geometry.LocalTriangles[index];
            var world = new PhysicsTriangle2D(
                Transform(local.First, transform),
                Transform(local.Second, transform),
                Transform(local.Third, transform)
            );
            geometry.WorldTriangles[index] = world;
            ExpandBounds(ref minimum, ref maximum, world.First);
            ExpandBounds(ref minimum, ref maximum, world.Second);
            ExpandBounds(ref minimum, ref maximum, world.Third);
        }
        geometry.WorldBounds = new(minimum, maximum);
    }

    /// <summary>Expands an axis-aligned range to contain one point.</summary>
    /// <param name="minimum">The range minimum to update.</param>
    /// <param name="maximum">The range maximum to update.</param>
    /// <param name="point">The point to include.</param>
    private static void ExpandBounds(
        ref Vector2D<float> minimum,
        ref Vector2D<float> maximum,
        Vector2D<float> point
    )
    {
        minimum = new(MathF.Min(minimum.X, point.X), MathF.Min(minimum.Y, point.Y));
        maximum = new(MathF.Max(maximum.X, point.X), MathF.Max(maximum.Y, point.Y));
    }

    /// <summary>Determines whether two cached triangle geometries intersect.</summary>
    /// <param name="first">The first geometry.</param>
    /// <param name="second">The second geometry.</param>
    /// <returns><see langword="true"/> when any triangle pair intersects.</returns>
    private static bool Overlaps(ColliderGeometry first, ColliderGeometry second)
    {
        if (!Intersects(first.WorldBounds, second.WorldBounds))
            return false;

        foreach (var left in first.WorldTriangles)
        foreach (var right in second.WorldTriangles)
            if (left.Overlaps(right))
                return true;
        return false;
    }

    /// <summary>Determines whether two axis-aligned bounds intersect or touch.</summary>
    /// <param name="first">The first bounds.</param>
    /// <param name="second">The second bounds.</param>
    /// <returns><see langword="true"/> when the bounds intersect.</returns>
    private static bool Intersects(PhysicsBounds2D first, PhysicsBounds2D second) =>
        first.Minimum.X <= second.Maximum.X
        && first.Maximum.X >= second.Minimum.X
        && first.Minimum.Y <= second.Maximum.Y
        && first.Maximum.Y >= second.Minimum.Y;

    /// <summary>Transforms a local 2D point to world space.</summary>
    /// <param name="value">The local point.</param>
    /// <param name="transform">The world transform.</param>
    /// <returns>The transformed point.</returns>
    private static Vector2D<float> Transform(
        Vector2D<float> value,
        Matrix4X4<float> transform
    ) =>
        new(
            value.X * transform.M11 + value.Y * transform.M21 + transform.M41,
            value.X * transform.M12 + value.Y * transform.M22 + transform.M42
        );

    /// <summary>Caches local and transformed geometry for one collider.</summary>
    private sealed class ColliderGeometry
    {
        /// <summary>Creates cached geometry from local-space triangles.</summary>
        /// <param name="localTriangles">The collider's local-space triangles.</param>
        internal ColliderGeometry(PhysicsTriangle2D[] localTriangles)
        {
            LocalTriangles = localTriangles;
            WorldTriangles = new PhysicsTriangle2D[localTriangles.Length];
        }

        /// <summary>Gets the collider's local-space triangles.</summary>
        internal PhysicsTriangle2D[] LocalTriangles { get; }

        /// <summary>Gets the transformed world-space triangles.</summary>
        internal PhysicsTriangle2D[] WorldTriangles { get; }

        /// <summary>Gets or sets the current world-space bounds.</summary>
        internal PhysicsBounds2D WorldBounds { get; set; }

        /// <summary>Gets or sets the previous step's world-space bounds.</summary>
        internal PhysicsBounds2D PreviousWorldBounds { get; set; }

        /// <summary>Gets or sets whether previous bounds are available.</summary>
        internal bool HasPreviousWorldBounds { get; set; }
    }

    /// <summary>Describes one collider pair and optional resolved contact.</summary>
    /// <param name="First">One collider in the reported pair.</param>
    /// <param name="Second">The other collider in the reported pair.</param>
    /// <param name="Contact">The optional swept-contact data.</param>
    internal readonly record struct CollisionResult(
        PhysicsCollider2D First,
        PhysicsCollider2D Second,
        PhysicsCollisionContact? Contact
    );
}

/// <summary>Converts between owner-local and world-space physics poses.</summary>
internal static class PhysicsPose
{
    /// <summary>Determines whether an owner's parent transform preserves rigid world-space poses.</summary>
    /// <param name="owner">The physics body owner.</param>
    /// <returns><see langword="true"/> when the parent is rigid and invertible.</returns>
    internal static bool IsSupportedParent(IGameObject2D owner)
    {
        var parent = owner.Parent as ISpatialObject;
        if (parent is null)
            return true;
        var transform = parent.WorldTransform;
        var xLengthSquared = transform.M11 * transform.M11 + transform.M12 * transform.M12;
        var yLengthSquared = transform.M21 * transform.M21 + transform.M22 * transform.M22;
        var dot = transform.M11 * transform.M21 + transform.M12 * transform.M22;
        var determinant = transform.M11 * transform.M22 - transform.M12 * transform.M21;
        return MathF.Abs(xLengthSquared - 1f) < 0.0001f
            && MathF.Abs(yLengthSquared - 1f) < 0.0001f
            && MathF.Abs(dot) < 0.0001f
            && determinant > 0f
            && MathF.Abs(determinant - 1f) < 0.0001f
            && float.IsFinite(transform.M41)
            && float.IsFinite(transform.M42);
    }

    /// <summary>Counts the scene hierarchy depth of a node.</summary>
    /// <param name="node">The node whose depth is measured.</param>
    /// <returns>The number of parent nodes.</returns>
    internal static int Depth(ISceneNode? node)
    {
        var depth = 0;
        while (node?.Parent is not null)
        {
            depth++;
            node = node.Parent;
        }
        return depth;
    }

    /// <summary>Writes a world-space pose to an owner's local transform.</summary>
    /// <param name="owner">The owner to update.</param>
    /// <param name="worldPosition">The desired world-space position.</param>
    /// <param name="worldRotation">The desired world-space rotation.</param>
    internal static void WriteLocal(
        IGameObject2D owner,
        Vector2D<float> worldPosition,
        float worldRotation
    )
    {
        var local = ToLocal(owner, worldPosition, worldRotation);
        owner.Position = local.Position;
        owner.Rotation = local.Rotation;
    }

    /// <summary>Converts a world-space pose to the owner's local transform.</summary>
    /// <param name="owner">The owner whose local pose is calculated.</param>
    /// <param name="worldPosition">The desired world-space position.</param>
    /// <param name="worldRotation">The desired world-space rotation.</param>
    /// <returns>The local position and rotation.</returns>
    internal static (Vector2D<float> Position, float Rotation) ToLocal(
        IGameObject2D owner,
        Vector2D<float> worldPosition,
        float worldRotation
    )
    {
        if (!IsSupportedParent(owner))
            throw new InvalidOperationException("Physics bodies require rigid, invertible parent transforms.");

        if (owner.Parent is not ISpatialObject parent)
            return (worldPosition, worldRotation);

        var transform = parent.WorldTransform;
        var parentRotation = MathF.Atan2(transform.M12, transform.M11);
        var delta = worldPosition - new Vector2D<float>(transform.M41, transform.M42);
        return (
            new(
                delta.X * transform.M11 + delta.Y * transform.M12,
                delta.X * transform.M21 + delta.Y * transform.M22
            ),
            worldRotation - parentRotation
        );
    }
}

/// <summary>Converts supported physics shapes into triangles for overlap testing.</summary>
internal static class PhysicsGeometry
{
    /// <summary>Triangulates a supported local-space physics shape.</summary>
    /// <param name="shape">The shape to triangulate.</param>
    /// <returns>The triangles that approximate or represent the shape.</returns>
    internal static IEnumerable<PhysicsTriangle2D> Triangulate(IPhysicsShape2D shape) =>
        shape switch
        {
            TriangleMeshShape2D mesh => mesh.Triangles,
            RectangleShape2D rectangle => Polygon(rectangle.LocalBounds),
            ConvexPolygonShape2D polygon => Fan(polygon.Vertices),
            CircleShape2D circle => Circle(circle),
            CapsuleShape2D capsule => Capsule(capsule),
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };

    /// <summary>Triangulates a rectangular polygon fan from its bounds.</summary>
    /// <param name="bounds">The rectangle bounds.</param>
    /// <returns>The rectangle's two triangles.</returns>
    private static IEnumerable<PhysicsTriangle2D> Polygon(PhysicsBounds2D bounds) =>
        Fan(
        [
            new(bounds.Minimum.X, bounds.Minimum.Y),
            new(bounds.Maximum.X, bounds.Minimum.Y),
            new(bounds.Maximum.X, bounds.Maximum.Y),
            new(bounds.Minimum.X, bounds.Maximum.Y),
        ]);

    /// <summary>Triangulates a convex polygon as a fan from its first vertex.</summary>
    /// <param name="vertices">The polygon vertices in winding order.</param>
    /// <returns>The generated triangles.</returns>
    private static IEnumerable<PhysicsTriangle2D> Fan(IReadOnlyList<Vector2D<float>> vertices)
    {
        for (var index = 1; index < vertices.Count - 1; index++)
            yield return new(vertices[0], vertices[index], vertices[index + 1]);
    }

    /// <summary>Approximates a circle with a triangle fan.</summary>
    /// <param name="circle">The circle to triangulate.</param>
    /// <returns>The generated triangles.</returns>
    private static IEnumerable<PhysicsTriangle2D> Circle(CircleShape2D circle)
    {
        const int segments = 24;
        for (var index = 0; index < segments; index++)
        {
            var first = index * MathF.Tau / segments;
            var second = (index + 1) * MathF.Tau / segments;
            yield return new(
                circle.Center,
                circle.Center + new Vector2D<float>(MathF.Cos(first), MathF.Sin(first)) * circle.Radius,
                circle.Center + new Vector2D<float>(MathF.Cos(second), MathF.Sin(second)) * circle.Radius
            );
        }
    }

    /// <summary>Approximates a capsule with a triangle fan.</summary>
    /// <param name="capsule">The capsule to triangulate.</param>
    /// <returns>The generated triangles.</returns>
    private static IEnumerable<PhysicsTriangle2D> Capsule(CapsuleShape2D capsule)
    {
        var direction = capsule.Second - capsule.First;
        var length = MathF.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        if (length == 0f)
            return Circle(new CircleShape2D(capsule.Radius, capsule.First));

        var unit = direction / length;
        var normal = new Vector2D<float>(-unit.Y, unit.X);
        const int segments = 12;
        var vertices = new List<Vector2D<float>>();
        for (var index = 0; index <= segments; index++)
        {
            var angle = MathF.PI / 2f + index * MathF.PI / segments;
            vertices.Add(
                capsule.First
                    + new Vector2D<float>(
                        unit.X * MathF.Cos(angle) + normal.X * MathF.Sin(angle),
                        unit.Y * MathF.Cos(angle) + normal.Y * MathF.Sin(angle)
                    ) * capsule.Radius
            );
        }
        for (var index = 0; index <= segments; index++)
        {
            var angle = -MathF.PI / 2f + index * MathF.PI / segments;
            vertices.Add(
                capsule.Second
                    + new Vector2D<float>(
                        unit.X * MathF.Cos(angle) + normal.X * MathF.Sin(angle),
                        unit.Y * MathF.Cos(angle) + normal.Y * MathF.Sin(angle)
                    ) * capsule.Radius
            );
        }
        return Fan(vertices).ToArray();
    }
}
