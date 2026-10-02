namespace Nexus.Graphics.Textures;

/// <summary>Provides shared textures used by graphics features when no content texture is assigned.</summary>
public static class BuiltInTextures
{
    /// <summary>Gets the magenta one-pixel texture used to represent an invalid or missing texture.</summary>
    public static readonly ITexture Invalid = new Texture(string.Empty, 1, 1, [Colors.Magenta]);

    /// <summary>Gets the white one-pixel texture used for uniform-color sampling.</summary>
    public static readonly ITexture Uniform = new Texture("uniform", 1, 1, [Colors.White]);
}
