namespace Nexus.Graphics.Text;

public readonly record struct TextStyleDescription(
    string FontFamily,
    ContentId FontResourceId,
    float Size,
    float WidthFactor = 1f,
    float OutlineWidth = 0f
);
