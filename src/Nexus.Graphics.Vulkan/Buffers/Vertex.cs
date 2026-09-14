namespace Nexus.Graphics.Vulkan.Buffers;

[StructLayout(LayoutKind.Sequential)]
public struct Vertex(float x, float y)
{
    public Vector2D<float> Position = new(x, y);
}
