namespace Nexus.Graphics.Textures;

/// <summary>
/// Source for loading texture data.
/// Implementations handle different texture formats and loading mechanisms.
/// </summary>
public interface ITexture
{
    public ContentId Id { get; }
    public uint Width { get; }
    public uint Height { get; }

    ResourceId ResourceId { get; }
    ulong Count { get; }
    ReadOnlyMemory<byte> GetPixelData(ColorFormatEnum format);
}
