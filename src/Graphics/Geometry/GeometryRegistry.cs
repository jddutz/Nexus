using Nexus.Core.Performance;
namespace Nexus.Graphics.Geometry;

/// <summary>
/// Provides access to the geometry instances built into the graphics system.
/// </summary>
public sealed class GeometryRegistry : IGeometryRegistry
{
    private readonly IGraphicsProfiler? _profiler;
    private readonly Dictionary<GeometryId, IGeometry> _geometries = [];

    /// <summary>
    /// Creates a registry containing all built-in geometry instances.
    /// </summary>
    public GeometryRegistry()
        : this(null) { }

    /// <summary>
    /// Creates a registry containing all built-in geometry instances.
    /// </summary>
    /// <param name="contentManifest">
    /// The optional manifest configuration reserved for content-backed geometry loading.
    /// </param>
    public GeometryRegistry(IOptions<IContentManifest>? contentManifest, IGraphicsProfiler? profiler = null)
    {
        _profiler = profiler;
        using var timing = new LoadPerformanceScope(profiler, "geometry.registry.initialize", units: 6);
        _ = contentManifest;
        Register(BuiltInGeometry.Empty);
        Register(BuiltInGeometry.FullScreenTriangle);
        Register(BuiltInGeometry.UniformColorRectCentered);
        Register(BuiltInGeometry.UniformColorRectOffset);
        Register(BuiltInGeometry.TexturedQuadCentered);
        Register(BuiltInGeometry.TexturedQuadOffset);
    }

    /// <inheritdoc/>
    public IGeometry GetOrCreate(ContentId contentId) =>
        throw new NotImplementedException(
            $"Creating geometry from content '{contentId}' is not implemented."
        );

    /// <inheritdoc/>
    public IGeometry Get(GeometryId geometry)
    {
        var found = _geometries.TryGetValue(geometry, out var value);
        _profiler?.RecordCache("geometry.registry", null, found);
        if (!found)
            throw new KeyNotFoundException($"Geometry '{geometry}' is not registered.");

        return value!;
    }

    /// <inheritdoc/>
    public IEnumerable<IGeometry> Update(GeometryId geometry) => [];

    /// <inheritdoc/>
    public IEnumerable<IGeometry> Release(GeometryId geometry) => [];

    /// <inheritdoc/>
    public void Reset() { }

    /// <inheritdoc/>
    public void Dispose() => GC.SuppressFinalize(this);

    /// <summary>
    /// Adds a geometry to the registry using its intrinsic identifier.
    /// </summary>
    /// <param name="geometry">The geometry to register.</param>
    private void Register(IGeometry geometry) => _geometries.Add(geometry.Id, geometry);
}
