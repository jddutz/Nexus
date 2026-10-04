namespace Nexus.Graphics;

/// <summary>Provides common render-layer masks.</summary>
public static class RenderLayers
{
    /// <summary>Gets the mask for the default UI layer in slot zero.</summary>
    public const ulong DefaultUI = 1UL;

    /// <summary>Gets a mask selecting every supported render-layer slot.</summary>
    public const ulong All = ulong.MaxValue;
}
