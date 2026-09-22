public interface IGeometryRegistry : IDisposable
{
    IEnumerable<IVulkanCommand> Create(IGeometry geometry);
    IEnumerable<IVulkanCommand> Update(IGeometry geometry);
    IEnumerable<IVulkanCommand> Release(IGeometry geometry);
    IEnumerable<IVulkanCommand> Reset();
}
