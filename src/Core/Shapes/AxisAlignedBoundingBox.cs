using Nexus.Core.Geometry;
using MathCircle = Silk.NET.Maths.Circle<float>;
using MathSphere = Silk.NET.Maths.Sphere<float>;

namespace Nexus.Core.Shapes;

/// <summary>
/// Defines a world-axis-aligned box relative to its owning object's world-space origin.
/// </summary>
public partial class AxisAlignedBoundingBox : Component, IShape
{
    /// <summary>Gets the minimum offset from the owner's world-space origin.</summary>
    [Observable]
    private Vector3D<float> _minimumOffset;

    /// <summary>Gets the maximum offset from the owner's world-space origin.</summary>
    [Observable]
    private Vector3D<float> _maximumOffset;

    /// <summary>Creates a zero-sized box at the owner's origin.</summary>
    public AxisAlignedBoundingBox() { }

    /// <summary>Creates a box with offsets in the X and Y directions and zero Z extent.</summary>
    public AxisAlignedBoundingBox(
        Vector2D<float> minimumOffset,
        Vector2D<float> maximumOffset
    )
        : this(
            new Vector3D<float>(minimumOffset.X, minimumOffset.Y, 0f),
            new Vector3D<float>(maximumOffset.X, maximumOffset.Y, 0f)
        )
    { }

    /// <summary>Creates a box with offsets in the X, Y, and Z directions.</summary>
    public AxisAlignedBoundingBox(
        Vector3D<float> minimumOffset,
        Vector3D<float> maximumOffset
    )
    {
        MinimumOffset = minimumOffset;
        MaximumOffset = maximumOffset;
    }

    /// <summary>Gets the box translated to its owner's current world-space origin.</summary>
    public Box3D<float> Bounds
    {
        get
        {
            var origin =
                Owner is ISpatialObject spatial
                    ? new Vector3D<float>(
                        spatial.WorldTransform.M41,
                        spatial.WorldTransform.M42,
                        spatial.WorldTransform.M43
                    )
                    : Vector3D<float>.Zero;

            return new Box3D<float>(origin + MinimumOffset, origin + MaximumOffset);
        }
    }

    /// <inheritdoc/>
    public bool IntersectsWith(Ray3D<float> value) => Intersection.Intersects(Bounds, value);

    /// <inheritdoc/>
    public bool IntersectsWith(Box2D<float> value) => Intersection.Intersects(Bounds, value);

    /// <inheritdoc/>
    public bool IntersectsWith(Box3D<float> value) => Intersection.Intersects(Bounds, value);

    /// <inheritdoc/>
    public bool IntersectsWith(Rectangle<float> value) => Intersection.Intersects(Bounds, value);

    /// <inheritdoc/>
    public bool IntersectsWith(MathCircle value) => Intersection.Intersects(Bounds, value);

    /// <inheritdoc/>
    public bool IntersectsWith(MathSphere value) => Intersection.Intersects(Bounds, value);

    /// <inheritdoc/>
    public bool IntersectsWith(Ray3D<float> ray, out float distance) => Intersection.Intersects(Bounds, ray, out distance);

    /// <inheritdoc/>
    public bool IntersectsWith(IShape other) => Intersection.Intersects(this, other);

}
