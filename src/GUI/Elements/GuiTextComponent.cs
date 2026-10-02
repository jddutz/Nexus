namespace Nexus.GUI.Elements;

using Nexus.Assets.Fonts;

/// <summary>Owns the prepared text drawable used by a GUI element.</summary>
internal sealed class GuiTextComponent : Nexus.Core.Component, IGraphicsComponent
{
    private TextSpan? _drawable;
    private IReadOnlyList<Nexus.Graphics.Drawables.IDrawable> _drawables =
        Array.Empty<Nexus.Graphics.Drawables.IDrawable>();
    private string _text = string.Empty;
    private ulong _renderLayerMask = ulong.MaxValue;

    /// <summary>Initializes a GUI text component with its shared text style.</summary>
    /// <param name="style">The style used to prepare glyph instances.</param>
    public GuiTextComponent(ITextStyle style)
    {
        Style = style ?? throw new ArgumentNullException(nameof(style));
    }

    /// <summary>Occurs when the text drawable is added.</summary>
    public event EventHandler<DrawableEventArgs>? DrawableAdded;

    /// <summary>Occurs when the text drawable is removed.</summary>
    public event EventHandler<DrawableEventArgs>? DrawableRemoved;

    /// <summary>Gets the currently exposed text drawable.</summary>
    public IReadOnlyList<Nexus.Graphics.Drawables.IDrawable> Drawables => _drawables;

    /// <summary>Gets the style used to prepare the text.</summary>
    public ITextStyle Style { get; }

    /// <summary>Gets or sets the text represented by the drawable.</summary>
    public string Text
    {
        get => _text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (_text == value)
                return;
            _text = value;
            RebuildDrawable();
        }
    }

    /// <summary>Gets or sets the render-layer mask applied to the drawable.</summary>
    public ulong RenderLayerMask
    {
        get => _renderLayerMask;
        set
        {
            if (_renderLayerMask == value)
                return;
            _renderLayerMask = value;
            if (_drawable is not null)
                _drawable.RenderLayerMask = value;
        }
    }

    /// <summary>Gets or sets normalized text alignment retained by the GUI layout.</summary>
    public Vector2D<float> Alignment { get; set; }

    /// <summary>Gets the visible bounds of the current text.</summary>
    public Rectangle<float> LayoutBounds => _drawable?.LayoutBounds ?? new Rectangle<float>();

    /// <summary>Gets or sets the world-space position of the text drawable.</summary>
    public Vector2D<float> Position
    {
        get =>
            _drawable is null
                ? Vector2D<float>.Zero
                : new(_drawable.TransformationMatrix.M41, _drawable.TransformationMatrix.M42);
        set
        {
            if (_drawable is null)
                return;
            var transform = _drawable.TransformationMatrix;
            transform.M41 = value.X;
            transform.M42 = value.Y;
            _drawable.TransformationMatrix = transform;
        }
    }

    /// <summary>Creates a prepared text span for GUI measurement.</summary>
    /// <param name="style">The style used to prepare glyph instances.</param>
    /// <param name="text">The text to prepare.</param>
    /// <returns>A text span containing the prepared glyph instances.</returns>
    public static TextSpan CreateSpan(ITextStyle style, string text)
    {
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(text);

        var instances = new List<(FontGlyph Glyph, Vector2D<float> Position, Color Color)>();
        var scale =
            style.FontMetrics.EmSize == 0 ? 1f : (float)(style.Size / style.FontMetrics.EmSize);
        var lineHeight = (float)(style.FontMetrics.LineHeight * scale);
        var baseline = (float)(style.FontMetrics.Ascender * scale);
        var positionX = 0f;
        var previousCodepoint = (int?)null;

        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value == '\n')
            {
                positionX = 0f;
                baseline += lineHeight;
                previousCodepoint = null;
                continue;
            }

            if (!style.Glyphs.TryGetValue(rune.Value, out var glyph))
                continue;

            if (
                previousCodepoint is int previous
                && style.Kerning.TryGetValue((previous, rune.Value), out var kerning)
            )
                positionX += (float)kerning * scale;

            instances.Add((glyph, new Vector2D<float>(positionX, baseline), style.Color));
            positionX += (float)glyph.Advance * scale;
            previousCodepoint = rune.Value;
        }

        return new TextSpan(style, instances);
    }

    /// <summary>Rebuilds the exposed drawable after text changes.</summary>
    private void RebuildDrawable()
    {
        var previous = _drawable;
        var drawable = CreateSpan(Style, _text);
        drawable.RenderLayerMask = _renderLayerMask;
        _drawable = drawable;
        _drawables = [drawable];
        if (previous is not null)
            DrawableRemoved?.Invoke(this, new DrawableEventArgs(previous));
        DrawableAdded?.Invoke(this, new DrawableEventArgs(drawable));
    }
}
