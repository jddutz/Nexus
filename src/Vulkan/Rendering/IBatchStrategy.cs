namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Provides an abstraction for batching strategies in the rendering pipeline.
/// Implementations define how render items are ordered and grouped to minimize Vulkan state changes.
/// </summary>
public interface IBatchStrategy : IComparer<IVulkanCommand> { }
