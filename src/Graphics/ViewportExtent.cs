namespace Nexus.Graphics;

/// <summary>
/// Describes the position, dimensions, and depth range of a viewport.
/// </summary>
public readonly record struct ViewportExtent(
    float X = 0f,
    float Y = 0f,
    float Width = 1f,
    float Height = 1f,
    float MinDepth = 0f,
    float MaxDepth = 1f
);
