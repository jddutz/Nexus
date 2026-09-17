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
            source: new TextureSource([Colors.Red, Colors.Green, Colors.Blue, Colors.White])
        );
}
