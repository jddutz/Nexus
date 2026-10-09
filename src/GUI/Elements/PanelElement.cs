namespace Nexus.GUI.Elements;

/// <summary>A layout panel whose atlas corners and edges retain their pixel sizes.</summary>
public sealed class PanelElement : Element
{
    private static readonly ITexture SelectionGlowTexture = CreateSelectionGlowTexture();
    private readonly NinePatchRenderer _selectionGlow;
    private bool _isSelected;
    private readonly NinePatchRenderer _renderer;
    private readonly List<IObservable> _ancestors = [];

    /// <summary>Shows a golden halo along the panel border when selected.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; SynchronizeRenderer(); }
    }

    /// <summary>Creates a panel from a named atlas region and its source border widths.</summary>
    /// <param name="atlas">The texture containing the panel artwork.</param>
    /// <param name="regionName">The named atlas region used for the frame.</param>
    /// <param name="sourceBorders">The left, top, right, and bottom border widths in source pixels.</param>
    public PanelElement(ITexture atlas, string regionName, Vector4D<float> sourceBorders)
    {
        var uv = atlas.GetRegion(regionName).TexCoords;
        _renderer = new NinePatchRenderer
        {
            IsVisible = false,
            Texture = atlas,
            TexCoord = new(uv.Origin.X, uv.Origin.Y, uv.Size.X, uv.Size.Y),
            SourceBorders = sourceBorders,
            BorderScale = 0.4f,
            RenderLayerMask = Nexus.Graphics.RenderLayers.DefaultUI,
        };
        AddComponent(_renderer);
        _selectionGlow = new NinePatchRenderer
        {
            Texture = SelectionGlowTexture,
            Color = new Color(1f, 0.68f, 0.2f),
            IsVisible = false,
            SourceBorders = new(16f, 16f, 16f, 16f),
            RenderLayerMask = Nexus.Graphics.RenderLayers.DefaultUI,
        };
        AddComponent(_selectionGlow);
        PropertyChanged += OnLayoutChanged;
    }

    private static ITexture CreateSelectionGlowTexture()
    {
        const int size = 64;
        var pixels = new Color[size * size];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var dx = MathF.Abs(x + 0.5f - size * 0.5f);
            var dy = MathF.Abs(y + 0.5f - size * 0.5f);
            // Fixed-size nine-patch corners keep the halo aligned with the frame
            // at every panel aspect ratio. The eight-pixel chamfer follows the frame.
            var distance = MathF.Max(MathF.Max(dx - 24f, dy - 24f),
                (dx + dy - 40f) / MathF.Sqrt(2f)) / 2f;
            var edgeFade = Math.Clamp((32f - MathF.Max(dx, dy)) / 3f, 0f, 1f);
            var alpha = 0.85f * MathF.Exp(-0.5f * distance * distance) * edgeFade;
            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
        }
        return new Texture(new ContentId("nexus.gui.panel.selection-glow"), size, size, pixels);
    }

    /// <inheritdoc />
    public override void Arrange(Rectangle<float> bounds)
    {
        base.Arrange(bounds);
        const float glowPadding = 4f;
        _selectionGlow.Destination = new(Bounds.Origin.X - glowPadding, Bounds.Origin.Y - glowPadding,
            Bounds.Size.X + glowPadding * 2f, Bounds.Size.Y + glowPadding * 2f);
        SynchronizeRenderer();
    }

    /// <inheritdoc />
    public override void OnSceneHierarchyChanged()
    {
        base.OnSceneHierarchyChanged();
        foreach (var ancestor in _ancestors)
            ancestor.PropertyChanged -= OnLayoutChanged;
        _ancestors.Clear();
        for (ISceneNode? node = Parent; node is not null; node = node.Parent)
            if (node is IObservable observable)
            {
                observable.PropertyChanged += OnLayoutChanged;
                _ancestors.Add(observable);
            }
        SynchronizeRenderer();
    }

    private void OnLayoutChanged(string propertyName)
    {
        if (propertyName is "" or nameof(Bounds) or nameof(IsVisible) or nameof(SortOrder))
            SynchronizeRenderer();
    }

    private void SynchronizeRenderer()
    {
        var visible = Bounds.Size.X > 0f && Bounds.Size.Y > 0f;
        for (ISceneNode? node = this; node is not null; node = node.Parent)
            if (node is IElement element && !element.IsVisible)
                visible = false;
        _renderer.IsVisible = false;
        _renderer.DrawOrder = SortOrder;
        _selectionGlow.DrawOrder = SortOrder - 1;
        _renderer.Destination = Bounds;
        _renderer.IsVisible = visible;
        _selectionGlow.IsVisible = visible && _isSelected;
    }
}
