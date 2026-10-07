namespace Nexus.Graphics;

/// <summary>Geometric clipping in normalized destination-image coordinates, with Y increasing down.</summary>
public enum ClippingMask
{
    None = 0,
    /// <summary>Keeps the diamond whose vertices are the image's edge midpoints.</summary>
    Diamond = 1,
    /// <summary>Keeps the entire upper half and tapers the lower half to the bottom midpoint.</summary>
    LowerHalfDiamond = 2,
}
