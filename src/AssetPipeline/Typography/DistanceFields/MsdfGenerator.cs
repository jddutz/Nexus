using System.Numerics;
using Nexus.AssetPipeline.Typography.Geometry;

namespace Nexus.AssetPipeline.Typography.DistanceFields;

/// <summary>
/// Generates multi-channel signed-distance bitmaps from generic font geometry.
/// </summary>
public sealed class MsdfGenerator
{
    private static readonly byte[] EdgeColorMasks = [0b011, 0b110, 0b101];

    /// <summary>
    /// Generates one RGB8 multi-channel signed-distance bitmap from closed contours.
    /// </summary>
    /// <param name="contours">The contours forming the glyph.</param>
    /// <param name="settings">The output resolution, distance range, and padding.</param>
    /// <returns>The generated glyph bitmap.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A setting is invalid.</exception>
    public GlyphBitmap Generate(IEnumerable<Contour> contours, MsdfGenerationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(contours);
        ArgumentNullException.ThrowIfNull(settings);
        if (!float.IsFinite(settings.PixelsPerUnit) || settings.PixelsPerUnit <= 0f)
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                "PixelsPerUnit must be finite and positive."
            );
        if (!float.IsFinite(settings.DistanceRange) || settings.DistanceRange <= 0f)
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                "DistanceRange must be finite and positive."
            );
        ArgumentOutOfRangeException.ThrowIfNegative(settings.Padding);

        var shape = contours.ToArray();
        if (shape.Any(contour => contour is null))
            throw new ArgumentException("Contours cannot contain null entries.", nameof(contours));

        if (GeometryBoundsCalculator.GetBounds(shape) is not { } geometryBounds)
            return new GlyphBitmap(0, 0, []);

        var geometryWidth = (geometryBounds.Right - geometryBounds.Left) * settings.PixelsPerUnit;
        var geometryHeight = (geometryBounds.Top - geometryBounds.Bottom) * settings.PixelsPerUnit;
        var width = checked((int)MathF.Ceiling(geometryWidth) + settings.Padding * 2);
        var height = checked((int)MathF.Ceiling(geometryHeight) + settings.Padding * 2);
        var pixels = new byte[checked(width * height * 3)];
        var masks = shape.Select(GetEdgeColorMasks).ToArray();
        var channelDistances = new float[3];

        for (var y = 0; y < height; y++)
        {
            var geometryY =
                geometryBounds.Top - (y - settings.Padding + 0.5f) / settings.PixelsPerUnit;
            for (var x = 0; x < width; x++)
            {
                var point = new Vector2(
                    geometryBounds.Left + (x - settings.Padding + 0.5f) / settings.PixelsPerUnit,
                    geometryY
                );
                Array.Fill(channelDistances, float.PositiveInfinity);

                for (var contourIndex = 0; contourIndex < shape.Length; contourIndex++)
                {
                    var contourEdges = shape[contourIndex].Edges;
                    for (var edgeIndex = 0; edgeIndex < contourEdges.Count; edgeIndex++)
                    {
                        var distance =
                            MathF.Abs(
                                GeometryDistance.SignedDistanceToEdge(
                                    point,
                                    contourEdges[edgeIndex]
                                )
                            ) * settings.PixelsPerUnit;
                        var mask = masks[contourIndex][edgeIndex];
                        for (var channel = 0; channel < 3; channel++)
                        {
                            if (
                                (mask & (1 << channel)) != 0
                                && distance < channelDistances[channel]
                            )
                                channelDistances[channel] = distance;
                        }
                    }
                }

                var signedShapeDistance =
                    GeometryDistance.SignedDistanceToShape(point, shape) * settings.PixelsPerUnit;
                var sign = signedShapeDistance < 0f ? -1f : 1f;
                var pixelOffset = (y * width + x) * 3;
                for (var channel = 0; channel < 3; channel++)
                {
                    var distance = float.IsPositiveInfinity(channelDistances[channel])
                        ? signedShapeDistance
                        : channelDistances[channel] * sign;
                    var normalized = Math.Clamp(
                        0.5f - distance / (2f * settings.DistanceRange),
                        0f,
                        1f
                    );
                    pixels[pixelOffset + channel] = (byte)MathF.Round(normalized * byte.MaxValue);
                }
            }
        }

        return new GlyphBitmap(width, height, pixels);
    }

    /// <summary>
    /// Assigns deterministic RGB channel masks to edge groups separated by geometric corners.
    /// </summary>
    /// <param name="contour">The contour whose edges are colored.</param>
    /// <returns>One three-channel mask for each ordered edge.</returns>
    private static byte[] GetEdgeColorMasks(Contour contour)
    {
        var edges = contour.Edges;
        if (edges.Count < 3)
            return Enumerable.Repeat((byte)0b111, edges.Count).ToArray();

        var corners = new List<int>();
        for (var edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
        {
            var previous = edges[(edgeIndex + edges.Count - 1) % edges.Count];
            var current = edges[edgeIndex];
            var incoming = previous is QuadraticSegment previousCurve
                ? previous.End - previousCurve.Control
                : previous.End - previous.Start;
            var outgoing = current is QuadraticSegment currentCurve
                ? currentCurve.Control - current.Start
                : current.End - current.Start;
            if (incoming.LengthSquared() == 0f || outgoing.LengthSquared() == 0f)
                continue;

            var cosine = Vector2.Dot(Vector2.Normalize(incoming), Vector2.Normalize(outgoing));
            if (cosine < 0.95f)
                corners.Add(edgeIndex);
        }

        if (corners.Count < 3)
            return Enumerable.Repeat((byte)0b111, edges.Count).ToArray();

        var masks = new byte[edges.Count];
        for (var groupIndex = 0; groupIndex < corners.Count; groupIndex++)
        {
            var start = corners[groupIndex];
            var end = corners[(groupIndex + 1) % corners.Count];
            var edgeIndex = start;
            do
            {
                masks[edgeIndex] = EdgeColorMasks[groupIndex % EdgeColorMasks.Length];
                edgeIndex = (edgeIndex + 1) % edges.Count;
            } while (edgeIndex != end);
        }

        return masks;
    }
}
