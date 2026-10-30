using Nexus.Core.Geometry;
using MathCircle = Silk.NET.Maths.Circle<float>;
using MathSphere = Silk.NET.Maths.Sphere<float>;

namespace Nexus.Core.Shapes;

/// <summary>Defines a box transformed by its owning object's world transform.</summary>
public partial class OrientedBoundingBox : Component, IShape
{
    [Observable]
    private Vector3D<float> _minimumOffset;

    [Observable]
    private Vector3D<float> _maximumOffset;

    /// <summary>Creates a zero-sized box at the owner's origin.</summary>
    public OrientedBoundingBox() { }

    /// <summary>Creates a box with offsets in the X and Y directions and zero Z extent.</summary>
    public OrientedBoundingBox(Vector2D<float> minimumOffset, Vector2D<float> maximumOffset)
        : this(
            new Vector3D<float>(minimumOffset.X, minimumOffset.Y, 0f),
            new Vector3D<float>(maximumOffset.X, maximumOffset.Y, 0f)
        )
    { }

    /// <summary>Creates a box with offsets in the X, Y, and Z directions.</summary>
    public OrientedBoundingBox(Vector3D<float> minimumOffset, Vector3D<float> maximumOffset)
    {
        MinimumOffset = minimumOffset;
        MaximumOffset = maximumOffset;
    }

    /// <summary>Gets the axis-aligned envelope enclosing this box in world space.</summary>
    public Box3D<float> Bounds => WorldGeometry.Bounds;

    /// <summary>Gets an independent snapshot of the transformed box.</summary>
    public OrientedBox WorldGeometry => new(MinimumOffset, MaximumOffset, Owner is ISpatialObject spatial ? spatial.WorldTransform : Matrix4X4<float>.Identity);

    /// <inheritdoc/>
    public bool IntersectsWith(Ray3D<float> value) => Intersection.Intersects(WorldGeometry, value);

    /// <inheritdoc/>
    public bool IntersectsWith(Box2D<float> value) => Intersection.Intersects(WorldGeometry, value);

    /// <inheritdoc/>
    public bool IntersectsWith(Box3D<float> value) => Intersection.Intersects(WorldGeometry, value);

    /// <inheritdoc/>
    public bool IntersectsWith(Rectangle<float> value) => Intersection.Intersects(WorldGeometry, value);

    /// <inheritdoc/>
    public bool IntersectsWith(MathCircle value) => Intersection.Intersects(WorldGeometry, value);

    /// <inheritdoc/>
    public bool IntersectsWith(MathSphere value) => Intersection.Intersects(WorldGeometry, value);

    /// <inheritdoc/>
    public bool IntersectsWith(Ray3D<float> ray, out float distance) => Intersection.Intersects(WorldGeometry, ray, out distance);

    /// <inheritdoc/>
    public bool IntersectsWith(IShape other) => Intersection.Intersects(this, other);

}
