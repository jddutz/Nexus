namespace Nexus.Graphics.Textures;

public static class BuiltInTextures
{
    public static Texture UniformColor =>
        new(
            name: nameof(UniformColor),
            width: 1,
            height: 1,
            source: new TextureSource([Colors.White])
        );

    public static Texture FourColorAtlas =>
        new(
            name: nameof(FourColorAtlas),
            width: 2,
            height: 2,
            source: new TextureSource([
                new Color(0.04f, 0.04f, 0.04f, 1.0f),
                new Color(0.025f, 0.025f, 0.025f, 1.0f),
                new Color(0.025f, 0.025f, 0.025f, 1.0f),
                new Color(0.01f, 0.01f, 0.01f, 1.0f),
            ])
        );
}
