namespace Nexus.Graphics.Textures;

/// <summary>
/// Source for loading texture data.
/// Implementations handle different texture formats and loading mechanisms.
/// </summary>
public interface ITexture
{
    TextureId Id { get; }
    public ContentId ContentId { get; }
    public uint Width { get; }
    public uint Height { get; }

    ulong Count { get; }

    /// <summary>Gets the pixel format used to store and sample this texture.</summary>
    ColorFormatEnum TextureFormat { get; }

    void WriteTo(ulong start, ulong count, ColorFormatEnum format, Span<byte> target);
}
