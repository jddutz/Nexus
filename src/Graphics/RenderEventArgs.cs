namespace Nexus.Graphics;

public class RenderEventArgs
{
    public uint ImageIndex { get; init; }

    public RenderEventArgs() { }

    public RenderEventArgs(uint imageIndex)
    {
        ImageIndex = imageIndex;
    }
}
