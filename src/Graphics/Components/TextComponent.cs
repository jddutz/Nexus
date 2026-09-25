using Nexus.Graphics.Text;

namespace Nexus.Graphics.Components;

/// <summary>
/// Groups text spans that share the component's lifetime.
/// </summary>
public class TextComponent : Component, IGraphicsComponent
{
    private readonly List<TextSpan> _spans = [];
    private readonly ITextStyle _textStyle;
    private string _text = string.Empty;

    /// <inheritdoc/>
    public event EventHandler<DrawableEventArgs>? DrawableAdded;

    /// <inheritdoc/>
    public event EventHandler<DrawableEventArgs>? DrawableRemoved;

    /// <summary>Initializes a text component with the style used by its spans.</summary>
    /// <param name="textStyle">The font and visual data used to render the component's text.</param>
    public TextComponent(ITextStyle textStyle)
    {
        _textStyle = textStyle ?? throw new ArgumentNullException(nameof(textStyle));
    }

    /// <summary>Gets or sets the text represented by this component.</summary>
    public string Text
    {
        get => _text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _text = value;

            var removedSpans = _spans.ToArray();
            _spans.Clear();

            // TODO: Parse multiline and rich text into glyph spans.
            var span = new TextSpan(_textStyle, _text);
            _spans.Add(span);

            foreach (var removedSpan in removedSpans)
                DrawableRemoved?.Invoke(this, new DrawableEventArgs(removedSpan));

            DrawableAdded?.Invoke(this, new DrawableEventArgs(span));
        }
    }

    /// <summary>Gets the spans as drawable contributions for the graphics system.</summary>
    public IReadOnlyList<IDrawable> Drawables => _spans;
}
