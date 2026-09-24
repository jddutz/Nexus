namespace Nexus.Graphics.Components;

/// <summary>
/// Groups text spans that share the component's lifetime.
/// </summary>
public class TextComponent : Component, IGraphicsComponent
{
    private readonly List<TextSpan> _spans = [];

    /// <summary>Initializes an empty text component.</summary>
    public TextComponent() { }

    /// <summary>Initializes a text component with the supplied spans.</summary>
    /// <param name="spans">The spans rendered by the component.</param>
    public TextComponent(IEnumerable<TextSpan> spans)
    {
        ArgumentNullException.ThrowIfNull(spans);
        _spans.AddRange(spans);
    }

    /// <summary>Gets the spans rendered by this component.</summary>
    public IReadOnlyList<TextSpan> Spans => _spans;

    /// <summary>Gets the spans as drawable contributions for the graphics system.</summary>
    public IReadOnlyList<IDrawable> Drawables => _spans.Cast<IDrawable>().ToArray();

    /// <summary>Adds a span to this text component.</summary>
    /// <param name="span">The span to add.</param>
    public void AddSpan(TextSpan span)
    {
        ArgumentNullException.ThrowIfNull(span);
        _spans.Add(span);
    }
}
