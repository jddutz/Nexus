namespace Nexus.Graphics.Shaders;

public static class InputSemantics
{
    public const int Transform = 0;
    public const int View = 1;
    public const int Projection = 2;
    public const int Color = 3;
    public const int TextureRegion = 4;

    /// <summary>Per-instance MSDF distance range in atlas pixels.</summary>
    public const int MsdfDistanceRange = 5;

    public static readonly int[] All =
    [
        Transform,
        View,
        Projection,
        Color,
        TextureRegion,
        MsdfDistanceRange,
    ];
}
