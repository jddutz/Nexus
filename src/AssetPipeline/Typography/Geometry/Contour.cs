using System.Collections.ObjectModel;

namespace Nexus.AssetPipeline.Typography.Geometry;

/// <summary>
/// Represents a closed contour made of generic geometry edges.
/// </summary>
public sealed class Contour
{
    private readonly ReadOnlyCollection<Edge> _edges;

    /// <summary>
    /// Initializes a contour from its ordered edges.
    /// </summary>
    /// <param name="edges">The edges that form the contour.</param>
    public Contour(IEnumerable<Edge> edges)
    {
        ArgumentNullException.ThrowIfNull(edges);
        _edges = Array.AsReadOnly(edges.ToArray());
    }

    /// <summary>
    /// Gets the ordered edges that form the contour.
    /// </summary>
    public IReadOnlyList<Edge> Edges => _edges;
}