namespace Nexus.GUI.Elements;

/// <summary>A layout panel whose atlas corners and edges retain their pixel sizes.</summary>
public sealed class PanelElement : Element
{
    private readonly ITexture _atlas;
    private readonly ITexture? _selectedAtlas;
    private readonly string _regionName;
    private bool _isSelected;
    private readonly NinePatchRenderer _renderer;
    private readonly List<IObservable> _ancestors = [];

    /// <summary>Uses the selected atlas artwork when selected.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; SynchronizeRenderer(); }
    }

    /// <summary>Creates a panel from a named atlas region and its source border widths.</summary>
    /// <param name="atlas">The texture containing the panel artwork.</param>
    /// <param name="regionName">The named atlas region used for the frame.</param>
    /// <param name="sourceBorders">The left, top, right, and bottom border widths in source pixels.</param>
    /// <param name="selectedAtlas">Optional selected artwork with matching region names.</param>
    public PanelElement(ITexture atlas, string regionName, Vector4D<float> sourceBorders, ITexture? selectedAtlas = null)
    {
        _atlas = atlas;
        _selectedAtlas = selectedAtlas;
        _regionName = regionName;
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
        PropertyChanged += OnLayoutChanged;
    }

    /// <inheritdoc />
    public override void Arrange(Rectangle<float> bounds)
    {
        base.Arrange(bounds);
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
        var atlas = _isSelected ? _selectedAtlas ?? _atlas : _atlas;
        var uv = atlas.GetRegion(_regionName).TexCoords;
        _renderer.Texture = atlas;
        _renderer.TexCoord = new(uv.Origin.X, uv.Origin.Y, uv.Size.X, uv.Size.Y);
        _renderer.Destination = Bounds;
        _renderer.IsVisible = visible;
    }
}
