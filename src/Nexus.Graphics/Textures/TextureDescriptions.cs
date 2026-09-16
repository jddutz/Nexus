namespace Nexus.Graphics.Textures;

public static class TextureDescriptions
{
    /// <summary>
    /// 1x1 white uniform color texture - used as default texture for solid color UI elements.
    /// This allows all UI elements to use the same uber-shader pipeline, eliminating pipeline switches.
    /// Elements without an explicit texture use this to enable tint color multiplication.
    /// </summary>
    public static readonly TextureDescription UniformColor = new("UniformColorTexture", 1, 1);
}
