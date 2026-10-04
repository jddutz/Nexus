using Nexus.Core;
using Nexus.Graphics.Geometry;

namespace Nexus.UnitTests.Graphics;

/// <summary>
/// Verifies lookup and lifecycle behavior for built-in geometry.
/// </summary>
public sealed class GeometryRegistryTests
{
    /// <summary>
    /// Verifies that every built-in geometry is registered by its identifier.
    /// </summary>
    [Fact]
    public void GetReturnsEveryBuiltInGeometry()
    {
        using var registry = new GeometryRegistry();

        var geometries = new[]
        {
            BuiltInGeometry.Empty,
            BuiltInGeometry.FullScreenTriangle,
            BuiltInGeometry.UniformColorRectCentered,
            BuiltInGeometry.UniformColorRectOffset,
            BuiltInGeometry.TexturedQuadCentered,
            BuiltInGeometry.TexturedQuadOffset,
        };

        foreach (var geometry in geometries)
            Assert.Equal(geometry.Id, registry.Get(geometry.Id).Id);
    }

    /// <summary>
    /// Verifies that looking up an unknown geometry identifier fails explicitly.
    /// </summary>
    [Fact]
    public void GetThrowsForUnregisteredGeometry()
    {
        using var registry = new GeometryRegistry();

        Assert.Throws<KeyNotFoundException>(() => registry.Get(GeometryId.Invalid));
    }

    /// <summary>
    /// Verifies that content-backed geometry creation remains an explicit scaffold.
    /// </summary>
    [Fact]
    public void CreateThrowsUntilContentLoadingIsImplemented()
    {
        using var registry = new GeometryRegistry();

        Assert.Throws<NotImplementedException>(() => registry.GetOrCreate((ContentId)"geometry"));
    }

    /// <summary>
    /// Verifies that lifecycle operations do not remove built-in geometry.
    /// </summary>
    [Fact]
    public void LifecycleOperationsDoNotChangeBuiltInGeometry()
    {
        using var registry = new GeometryRegistry();
        var geometryId = BuiltInGeometry.TexturedQuadOffset.Id;

        Assert.Empty(registry.Update(geometryId));
        Assert.Empty(registry.Release(geometryId));

        registry.Reset();

        Assert.Equal(geometryId, registry.Get(geometryId).Id);
    }
}
