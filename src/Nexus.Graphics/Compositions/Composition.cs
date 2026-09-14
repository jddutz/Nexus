namespace Nexus.Graphics.Compositions;

public class Composition : IGraphicsResource
{
    public ResourceId Id { get; init; }
    public IGraphicsResource? Background { get; set; }
}
