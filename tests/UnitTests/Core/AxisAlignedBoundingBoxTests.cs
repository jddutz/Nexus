using Nexus.Core;
using Nexus.Core.Shapes;
using Silk.NET.Maths;

namespace Tests;

public class AxisAlignedBoundingBoxTests
{
    [Fact]
    public void TwoDimensionalConstructorSetsZeroZExtent()
    {
        var box = new AxisAlignedBoundingBox(
            new Vector2D<float>(-2f, -3f),
            new Vector2D<float>(4f, 5f)
        );

        Assert.Equal(new Vector3D<float>(-2f, -3f, 0f), box.MinimumOffset);
        Assert.Equal(new Vector3D<float>(4f, 5f, 0f), box.MaximumOffset);
    }

    [Fact]
    public void BoundsUseOwnerWorldOriginWithoutRotatingOrScalingOffsets()
    {
        var owner = new GameObject3D
        {
            Position = new Vector3D<float>(10f, 20f, 30f),
            Scale = new Vector3D<float>(2f, 2f, 2f),
            Quaternion = Quaternion<float>.CreateFromAxisAngle(Vector3D<float>.UnitZ, MathF.PI / 2f),
        };
        var box = new AxisAlignedBoundingBox(
            new Vector3D<float>(-1f, -2f, -3f),
            new Vector3D<float>(4f, 5f, 6f)
        )
        {
            Owner = owner,
        };

        Assert.Equal(
            new Box3D<float>(
                new Vector3D<float>(9f, 18f, 27f),
                new Vector3D<float>(14f, 25f, 36f)
            ),
            box.Bounds
        );
    }

    [Fact]
    public void IntersectionQueriesIncludeBoundaryContact()
    {
        var box = new AxisAlignedBoundingBox(
            new Vector3D<float>(-1f, -1f, -1f),
            new Vector3D<float>(1f, 1f, 1f)
        );

        Assert.True(box.IntersectsWith(new Box2D<float>(1f, -0.5f, 2f, 0.5f)));
        Assert.True(
            box.IntersectsWith(
                new Box3D<float>(new Vector3D<float>(1f, 0f, 0f), new Vector3D<float>(2f, 1f, 1f))
            )
        );
        Assert.True(
            box.IntersectsWith(
                new Ray3D<float>(new Vector3D<float>(-2f, 0f, 0f), Vector3D<float>.UnitX),
                out var distance
            )
        );
        Assert.Equal(1f, distance);
    }

    [Fact]
    public void RectangleIntersectionUsesWorldSpaceXYBounds()
    {
        var box = new AxisAlignedBoundingBox(
            new Vector3D<float>(-1f, -1f, -1f),
            new Vector3D<float>(1f, 1f, 1f)
        );

        Assert.True(box.IntersectsWith(new Rectangle<float>(1f, -0.5f, 1f, 1f)));
        Assert.False(box.IntersectsWith(new Rectangle<float>(2f, -0.5f, 1f, 1f)));
    }

    [Fact]
    public void CircleIntersectionUsesClosestXYPoint()
    {
        var box = new AxisAlignedBoundingBox(
            new Vector3D<float>(-1f, -1f, -1f),
            new Vector3D<float>(1f, 1f, 1f)
        );

        Assert.True(box.IntersectsWith(new Circle<float>(new Vector2D<float>(1.5f, 0f), 0.5f)));
        Assert.False(box.IntersectsWith(new Circle<float>(new Vector2D<float>(1.6f, 0f), 0.5f)));
    }

    [Fact]
    public void SphereIntersectionUsesClosestWorldPoint()
    {
        var box = new AxisAlignedBoundingBox(
            new Vector3D<float>(-1f, -1f, -1f),
            new Vector3D<float>(1f, 1f, 1f)
        );

        Assert.True(box.IntersectsWith(new Sphere<float>(new Vector3D<float>(1.5f, 0f, 0f), 0.5f)));
        Assert.False(box.IntersectsWith(new Sphere<float>(new Vector3D<float>(1.6f, 0f, 0f), 0.5f)));
    }

    [Fact]
    public void RayMissReturnsFalseAndZeroDistance()
    {
        var box = new AxisAlignedBoundingBox(
            new Vector3D<float>(-1f, -1f, -1f),
            new Vector3D<float>(1f, 1f, 1f)
        );

        Assert.False(
            box.IntersectsWith(
                new Ray3D<float>(new Vector3D<float>(-2f, 2f, 0f), Vector3D<float>.UnitX),
                out var distance
            )
        );
        Assert.Equal(0f, distance);
    }
}
