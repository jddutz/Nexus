using Nexus.Core;
using Nexus.Core.Shapes;
using Silk.NET.Maths;

namespace Tests;

public class OrientedBoundingBoxTests
{
    [Fact]
    public void WorldCornersUseOwnersFullWorldTransform()
    {
        var owner = new GameObject3D
        {
            Position = new Vector3D<float>(10f, 20f, 30f),
            Scale = new Vector3D<float>(2f, 3f, 4f),
            Quaternion = Quaternion<float>.CreateFromAxisAngle(Vector3D<float>.UnitZ, MathF.PI / 2f),
        };
        var box = new OrientedBoundingBox(
            new Vector3D<float>(-1f, -2f, -3f),
            new Vector3D<float>(1f, 2f, 3f)
        )
        {
            Owner = owner,
        };

        var bounds = box.Bounds;
        AssertVectorNear(new Vector3D<float>(4f, 18f, 18f), bounds.Min);
        AssertVectorNear(new Vector3D<float>(16f, 22f, 42f), bounds.Max);
    }

    [Fact]
    public void RayIntersectionUsesOrientedLocalBox()
    {
        var owner = new GameObject3D
        {
            Quaternion = Quaternion<float>.CreateFromAxisAngle(Vector3D<float>.UnitZ, MathF.PI / 4f),
        };
        var box = new OrientedBoundingBox(
            new Vector3D<float>(-1f, -0.5f, -0.5f),
            new Vector3D<float>(1f, 0.5f, 0.5f)
        )
        {
            Owner = owner,
        };
        var ray = new Ray3D<float>(new Vector3D<float>(-2f, 0f, 0f), Vector3D<float>.UnitX);

        Assert.True(box.IntersectsWith(ray, out var distance));
        Assert.Equal(2f - MathF.Sqrt(0.5f), distance, 5);
    }

    [Fact]
    public void BoxIntersectionDoesNotUseOnlyWorldAabbEnvelope()
    {
        var owner = new GameObject3D
        {
            Quaternion = Quaternion<float>.CreateFromAxisAngle(Vector3D<float>.UnitZ, MathF.PI / 4f),
        };
        var box = new OrientedBoundingBox(
            new Vector3D<float>(-1f, -0.1f, -0.1f),
            new Vector3D<float>(1f, 0.1f, 0.1f)
        )
        {
            Owner = owner,
        };
        var separated = new Box3D<float>(
            new Vector3D<float>(-0.1f, 0.7f, -0.05f),
            new Vector3D<float>(0.1f, 0.75f, 0.05f)
        );

        Assert.True(box.Bounds.Max.Y >= separated.Min.Y);
        Assert.False(box.IntersectsWith(separated));
    }

    [Fact]
    public void TwoDimensionalConstructorSetsZeroZExtent()
    {
        var box = new OrientedBoundingBox(
            new Vector2D<float>(-2f, -3f),
            new Vector2D<float>(4f, 5f)
        );

        Assert.Equal(new Vector3D<float>(-2f, -3f, 0f), box.MinimumOffset);
        Assert.Equal(new Vector3D<float>(4f, 5f, 0f), box.MaximumOffset);
    }

    [Fact]
    public void RectangleIntersectionUsesOrientedXYProjection()
    {
        var owner = new GameObject3D
        {
            Quaternion = Quaternion<float>.CreateFromAxisAngle(Vector3D<float>.UnitZ, MathF.PI / 4f),
        };
        var box = new OrientedBoundingBox(
            new Vector3D<float>(-1f, -1f, 0f),
            new Vector3D<float>(1f, 1f, 0f)
        )
        {
            Owner = owner,
        };

        Assert.True(box.IntersectsWith(new Rectangle<float>(0.6f, 0.6f, 0.1f, 0.1f)));
        Assert.False(box.IntersectsWith(new Rectangle<float>(1f, 1f, 0.1f, 0.1f)));
    }

    [Fact]
    public void CircleIntersectionUsesOrientedXYProjection()
    {
        var owner = new GameObject3D
        {
            Quaternion = Quaternion<float>.CreateFromAxisAngle(Vector3D<float>.UnitZ, MathF.PI / 4f),
        };
        var box = new OrientedBoundingBox(
            new Vector3D<float>(-1f, -1f, 0f),
            new Vector3D<float>(1f, 1f, 0f)
        )
        {
            Owner = owner,
        };

        Assert.True(box.IntersectsWith(new Circle<float>(new Vector2D<float>(0.75f, 0.75f), 0.1f)));
        Assert.False(box.IntersectsWith(new Circle<float>(new Vector2D<float>(1f, 1f), 0.1f)));
    }

    [Fact]
    public void SphereIntersectionUsesOrientedWorldGeometry()
    {
        var owner = new GameObject3D
        {
            Quaternion = Quaternion<float>.CreateFromAxisAngle(Vector3D<float>.UnitZ, MathF.PI / 4f),
        };
        var box = new OrientedBoundingBox(
            new Vector3D<float>(-1f, -0.1f, -0.1f),
            new Vector3D<float>(1f, 0.1f, 0.1f)
        )
        {
            Owner = owner,
        };

        Assert.True(
            box.IntersectsWith(new Sphere<float>(new Vector3D<float>(0f, 0f, 0f), 0.05f))
        );
        Assert.False(
            box.IntersectsWith(new Sphere<float>(new Vector3D<float>(0f, 0.8f, 0f), 0.05f))
        );
    }

    private static void AssertVectorNear(Vector3D<float> expected, Vector3D<float> actual)
    {
        Assert.InRange(MathF.Abs(expected.X - actual.X), 0f, 1e-5f);
        Assert.InRange(MathF.Abs(expected.Y - actual.Y), 0f, 1e-5f);
        Assert.InRange(MathF.Abs(expected.Z - actual.Z), 0f, 1e-5f);
    }
}
