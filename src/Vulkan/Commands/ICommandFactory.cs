namespace Nexus.Graphics.Vulkan.Commands;

/// <summary>Creates, updates, and releases Vulkan commands and resources for drawables.</summary>
public interface ICommandFactory
{
    /// <summary>Creates the Vulkan allocation and command set for a drawable.</summary>
    /// <param name="drawable">The drawable to allocate.</param>
    IEnumerable<IVulkanCommand> Create(IDrawable drawable);

    /// <summary>Updates the drawable's instance buffer and draw command.</summary>
    /// <param name="drawable">The drawable whose instance data changed.</param>
    IEnumerable<IVulkanCommand> UpdateInstanceData(IDrawable drawable);

    /// <summary>Updates the drawable's uniform-buffer bindings.</summary>
    /// <param name="drawable">The drawable whose uniform data changed.</param>
    IEnumerable<IVulkanCommand> UpdateUniformData(IDrawable drawable);

    /// <summary>Updates the drawable's image and sampler resources.</summary>
    /// <param name="drawable">The drawable whose texture state changed.</param>
    IEnumerable<IVulkanCommand> UpdateTexture(IDrawable drawable);

    /// <summary>Updates the drawable's vertex-buffer binding.</summary>
    /// <param name="drawable">The drawable whose mesh changed.</param>
    IEnumerable<IVulkanCommand> UpdateMesh(IDrawable drawable);

    /// <summary>Updates the drawable's pipeline and shader-dependent commands.</summary>
    /// <param name="drawable">The drawable whose shader contracts changed.</param>
    IEnumerable<IVulkanCommand> UpdateShaders(IDrawable drawable);

    /// <summary>Releases the Vulkan resources owned by a drawable.</summary>
    /// <param name="drawable">The drawable to release.</param>
    IEnumerable<IVulkanCommand> Release(IDrawable drawable);
}
