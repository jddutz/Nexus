namespace Nexus.Graphics.Text;

/// <summary>
/// Describes the font resource and size used to create a text style.
/// </summary>
/// <param name="FontFamily">The logical font family name.</param>
/// <param name="FontResourceId">The font content identifier.</param>
/// <param name="Size">The requested text size.</param>
/// <param name="WidthFactor">
/// The requested horizontal width factor. Unsupported until text layout support is implemented.
/// TODO: Apply this value during text layout.
/// </param>
/// <param name="OutlineWidth">
/// The requested outline width. Unsupported until outline rendering support is implemented.
/// TODO: Apply this value during font generation and text rendering.
/// </param>
public readonly record struct TextStyleDescription(
    string FontFamily,
    ContentId FontResourceId,
    float Size,
    float WidthFactor = 1f,
    float OutlineWidth = 0f
);
