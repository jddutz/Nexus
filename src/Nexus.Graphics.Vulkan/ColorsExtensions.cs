namespace Nexus.Graphics.Vulkan;

public static class ColorsExtensions
{
    public static ClearValue ClearValue(this Color color) =>
        new(new ClearColorValue(color.R, color.G, color.B, color.A));
}
