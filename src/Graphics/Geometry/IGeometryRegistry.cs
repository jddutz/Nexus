namespace Nexus.Graphics.Geometry;

public interface IGeometryRegistry : IDisposable
{
    IGeometry GetOrCreate(ContentId contentId);
    IGeometry Get(GeometryId geometry);
    IEnumerable<IGeometry> Update(GeometryId geometry);
    IEnumerable<IGeometry> Release(GeometryId geometry);

    void Reset();
}
