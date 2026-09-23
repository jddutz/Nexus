namespace Nexus.Graphics;

public class RenderLayer(int index, string name, uint renderPassMask) : IRenderLayer
{
    public int Index { get; } = index;
    public string Name { get; } = name;
    public uint RenderPassMask { get; set; } = renderPassMask;
}
