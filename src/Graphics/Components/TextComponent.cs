namespace Nexus.Graphics.Components;

/// <summary>
/// Groups text spans that share the component's lifetime.
/// </summary>
public class TextComponent : Component, IGraphicsComponent
{
    private readonly List<TextSpan> _spans = [];
    private string _text = string.Empty;

    /// <summary>Gets or sets the text represented by this component.</summary>
    public string Text
    {
        get => _text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _text = value;
            _spans.Clear();

            // TODO: Parse multiline and rich text into glyph spans.
            _spans.Add(new TextSpan(Texture.Invalid, []));
        }
    }

    /// <summary>Gets the spans as drawable contributions for the graphics system.</summary>
    public IReadOnlyList<IDrawable> Drawables => _spans;
}
