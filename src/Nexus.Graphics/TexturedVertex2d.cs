namespace Nexus.Graphics;

/// <summary>
/// A vertex for texture-mapped quad geometry, pairing a 2D position with a texture coordinate.
/// </summary>
public readonly struct TexturedVertex2d(float x, float y, float u, float v)
{
    public readonly float X = x;
    public readonly float Y = y;
    public readonly float U = u;
    public readonly float V = v;
}
