namespace Nexus.Graphics.Resources;

public interface IGraphicsResourceManager
{
    /// <summary>
    /// Resets the current state of the GraphicsSystem and loads the specified resources.
    /// </summary>
    void Load(IGraphicsResource resource);
}
