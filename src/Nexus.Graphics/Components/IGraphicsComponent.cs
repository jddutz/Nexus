namespace Nexus.Graphics.Components;

public interface IGraphicsComponent : IComponent
{
    IEnumerable<RenderLayer> RenderLayers { get; }
}
