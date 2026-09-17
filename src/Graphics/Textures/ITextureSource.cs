namespace Nexus.Graphics.Textures;

/// <summary>
/// Source for loading texture data.
/// Implementations handle different texture formats and loading mechanisms.
/// </summary>
public interface ITextureSource
{
    ResourceId Id { get; }
    ulong Count { get; }

    ReadOnlyMemory<byte> GetPixelData(ColorFormatEnum format);
}
