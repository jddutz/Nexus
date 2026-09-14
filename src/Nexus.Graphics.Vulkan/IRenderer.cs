namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Defines the contract for a renderer that executes Vulkan rendering operations.
/// </summary>
public interface IRenderer
{
    /// <summary>
    /// Gets or sets the current set of RenderDefinitions for rendering.
    /// </summary>
    RenderBatch[] Batches { get; set; }

    /// <summary>
    /// Determines whether the renderer is configured and able to execute rendering.
    /// </summary>
    /// <returns><see langword="true"/> if the renderer can execute rendering; otherwise, <see langword="false"/>.</returns>
    bool CanRender();

    /// <summary>
    /// Executes the rendering pipeline for the current frame.
    /// </summary>
    /// <returns><see langword="true"/> if the rendering pipeline was successfully executed;
    /// otherwise, <see langword="false"/>.</returns>
    bool Render();
}
