using System.Numerics;
using Nexus.Assets.Fonts;
using Nexus.Assets.Typography.Geometry;

namespace Nexus.AssetPipeline.Tests;

public sealed class QuadraticContourSimplifierTests
{
    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(48)]
    [InlineData(256)]
    public void ToleranceMaintainsPixelBudget(int emSize)
    {
        var settings = new FontGenerationSettings { EmSize = emSize };
        var tolerance = settings.GetOutlineTolerance(1000);
        Assert.InRange(MathF.Abs(tolerance * emSize / 1000f - FontGenerationSettings.OutlinePixelTolerance), 0, 1e-8f);
    }

    [Fact]
    public void MergingBoundsTheEntireCurveAndPreservesClosure()
    {
        var left = new QuadraticSegment(new(0, 0), new(.5f, 1.02f), new(1, 1));
        var right = new QuadraticSegment(new(1, 1), new(1.5f, 1.02f), new(2, 0));
        var closing = new LineSegment(new(2, 0), new(0, 0));
        var original = new Contour([left, right, closing]);
        var simplified = QuadraticContourSimplifier.Simplify(original, .025f);
        Assert.Equal(2, simplified.Edges.Count);
        Assert.Same(closing, simplified.Edges[1]);
        var curve = Assert.IsType<QuadraticSegment>(simplified.Edges[0]);
        for (var i = 0; i <= 1000; i++)
        {
            var t = i / 1000f;
            var reference = t <= .5f ? Evaluate(left, 2f * t) : Evaluate(right, 2f * t - 1f);
            Assert.InRange(Vector2.Distance(reference, Evaluate(curve, t)), 0, .0251f);
        }
        Assert.Same(original, QuadraticContourSimplifier.Simplify(original, .001f));
        Assert.Same(original, QuadraticContourSimplifier.Simplify(original, 0));
    }

    private static Vector2 Evaluate(QuadraticSegment curve, float t) =>
        (1 - t) * (1 - t) * curve.Start + 2 * (1 - t) * t * curve.Control + t * t * curve.End;
}
