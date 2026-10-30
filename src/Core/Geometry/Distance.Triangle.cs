namespace Nexus.Core.Geometry;

public static partial class Distance
{
    public static float SquaredToTriangle(
        Vector3D<float> point,
        Vector3D<float> first,
        Vector3D<float> second,
        Vector3D<float> third
    )
    {
        var firstEdge = second - first;
        var secondEdge = third - first;
        if (Vector3D.Cross(firstEdge, secondEdge).LengthSquared == 0f)
        {
            return MathF.Min(
                Squared(point, first, second),
                MathF.Min(
                    Squared(point, second, third),
                    Squared(point, third, first)
                )
            );
        }

        var fromFirst = point - first;
        var firstDot = Vector3D.Dot(firstEdge, fromFirst);
        var secondDot = Vector3D.Dot(secondEdge, fromFirst);
        if (firstDot <= 0f && secondDot <= 0f)
            return (point - first).LengthSquared;

        var fromSecond = point - second;
        var thirdDot = Vector3D.Dot(firstEdge, fromSecond);
        var fourthDot = Vector3D.Dot(secondEdge, fromSecond);
        if (thirdDot >= 0f && fourthDot <= thirdDot)
            return (point - second).LengthSquared;

        var firstRegion = firstDot * fourthDot - thirdDot * secondDot;
        if (firstRegion <= 0f && firstDot >= 0f && thirdDot <= 0f)
        {
            var fraction = firstDot / (firstDot - thirdDot);
            var closest = first + firstEdge * fraction;
            return (point - closest).LengthSquared;
        }

        var fromThird = point - third;
        var fifthDot = Vector3D.Dot(firstEdge, fromThird);
        var sixthDot = Vector3D.Dot(secondEdge, fromThird);
        if (sixthDot >= 0f && fifthDot <= sixthDot)
            return (point - third).LengthSquared;

        var secondRegion = fifthDot * secondDot - firstDot * sixthDot;
        if (secondRegion <= 0f && secondDot >= 0f && sixthDot <= 0f)
        {
            var fraction = secondDot / (secondDot - sixthDot);
            var closest = first + secondEdge * fraction;
            return (point - closest).LengthSquared;
        }

        var thirdRegion = thirdDot * sixthDot - fifthDot * fourthDot;
        if (
            thirdRegion <= 0f
            && fourthDot - thirdDot >= 0f
            && fifthDot - sixthDot >= 0f
        )
        {
            var fraction =
                (fourthDot - thirdDot)
                / (fourthDot - thirdDot + fifthDot - sixthDot);
            var closest = second + (third - second) * fraction;
            return (point - closest).LengthSquared;
        }

        var reciprocal = 1f / (firstRegion + secondRegion + thirdRegion);
        var firstFraction = secondRegion * reciprocal;
        var secondFraction = thirdRegion * reciprocal;
        var faceClosest = first + firstEdge * firstFraction + secondEdge * secondFraction;
        return (point - faceClosest).LengthSquared;
    }

}
