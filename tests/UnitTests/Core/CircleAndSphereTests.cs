using Nexus.Core;
using Nexus.Core.Shapes;
using Silk.NET.Maths;
using MathCircle = Silk.NET.Maths.Circle<float>;
using MathSphere = Silk.NET.Maths.Sphere<float>;

namespace Tests;

public class CircleAndSphereTests
{
    [Fact]
    public void CircleBoundsTransformCenterAndUseMaximumProjectedScale()
    {
        var owner = new GameObject3D
        {
            Position = new Vector3D<float>(4f, 5f, 6f),
            Scale = new Vector3D<float>(2f, 3f, 1f),
        };
        var circle = new Circle(new Vector2D<float>(1f, 1f), 2f) { Owner = owner };

        Assert.Equal(new Vector2D<float>(6f, 8f), circle.Bounds.Center);
        Assert.Equal(6f, circle.Bounds.Radius);
    }

    [Fact]
    public void CircleIntersectionsHandleRectanglesCirclesAndRays()
    {
        var circle = new Circle(new Vector2D<float>(0f, 0f), 1f);

        Assert.True(circle.IntersectsWith(new Rectangle<float>(0.5f, 0.5f, 1f, 1f)));
        Assert.False(circle.IntersectsWith(new Rectangle<float>(1f, 1f, 1f, 1f)));
        Assert.True(circle.IntersectsWith(new MathCircle(new Vector2D<float>(2f, 0f), 1f)));
        Assert.True(
            circle.IntersectsWith(
                new Ray3D<float>(new Vector3D<float>(0f, 0f, 2f), -Vector3D<float>.UnitZ),
                out var distance
            )
        );
        Assert.Equal(2f, distance);
    }

    [Fact]
    public void SphereBoundsTransformCenterAndUseMaximumScale()
    {
        var owner = new GameObject3D
        {
            Position = new Vector3D<float>(4f, 5f, 6f),
            Scale = new Vector3D<float>(2f, 3f, 4f),
        };
        var sphere = new Sphere(new Vector3D<float>(1f, 1f, 1f), 2f) { Owner = owner };

        Assert.Equal(new Vector3D<float>(6f, 8f, 10f), sphere.Bounds.Center);
        Assert.Equal(8f, sphere.Bounds.Radius);
    }

    [Fact]
    public void SphereIntersectionsHandleBoxesSpheresAndRays()
    {
        var sphere = new Sphere(Vector3D<float>.Zero, 1f);

        Assert.True(
            sphere.IntersectsWith(
                new Box3D<float>(new Vector3D<float>(1f, 0f, 0f), new Vector3D<float>(2f, 1f, 1f))
            )
        );
        Assert.True(
            sphere.IntersectsWith(
                new MathSphere(new Vector3D<float>(2f, 0f, 0f), 1f)
            )
        );
        Assert.True(
            sphere.IntersectsWith(
                new Ray3D<float>(new Vector3D<float>(-2f, 0f, 0f), Vector3D<float>.UnitX),
                out var distance
            )
        );
        Assert.Equal(1f, distance);
    }
}
