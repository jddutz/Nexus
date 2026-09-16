namespace Nexus.Graphics.Textures;

/// <summary>
/// Source for loading texture data.
/// Implementations handle different texture formats and loading mechanisms.
/// </summary>
public interface ITextureSource
{
    ReadOnlyMemory<byte> GetPixelData(PixelFormatEnum format);
}
