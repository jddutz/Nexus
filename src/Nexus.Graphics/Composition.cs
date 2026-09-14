namespace Nexus.Graphics;

public class Composition : IGraphicsResource
{
    public ResourceId Id { get; init; }
    public IGraphicsResource? Background { get; set; }
}
