namespace Nexus.Graphics.Vulkan;

public static class ColorsExtensions
{
    public static ClearValue ClearValue(this Vector4D<float> color) =>
        new(new ClearColorValue(color.X, color.Y, color.Z, color.W));
}
