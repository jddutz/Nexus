namespace Nexus.GUI.Elements;

/// <summary>A layout panel whose texture corners and edges retain their pixel sizes.</summary>
public sealed class PanelElement : Element
{
    private ITexture? _texture;
    private ITexture? _selectedTexture;
    private string? _regionName;
    private Vector4D<float> _sourceBorders;
    private bool _isSelected;
    private readonly NinePatchRenderer _renderer;
    private readonly List<IObservable> _ancestors = [];

    /// <summary>Gets or sets the texture containing the panel artwork.</summary>
    public ITexture? Texture
    {
        get => _texture;
        set
        {
            _texture = value;
            SynchronizeRenderer();
        }
    }

    /// <summary>Gets or sets optional artwork used when the panel is selected.</summary>
    public ITexture? SelectedTexture
    {
        get => _selectedTexture;
        set
        {
            _selectedTexture = value;
            SynchronizeRenderer();
        }
    }

    /// <summary>Gets or sets the named atlas region used for the panel artwork.</summary>
    public string? RegionName
    {
        get => _regionName;
        set
        {
            _regionName = value;
            SynchronizeRenderer();
        }
    }

    /// <summary>Gets or sets the source pixel widths of the panel's left, top, right, and bottom borders.</summary>
    public Vector4D<float> SourceBorders
    {
        get => _sourceBorders;
        set
        {
            _sourceBorders = value;
            SynchronizeRenderer();
        }
    }

    /// <summary>Uses the selected atlas artwork when selected.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            SynchronizeRenderer();
        }
    }

    /// <summary>Initializes an empty panel with an owned nine-patch renderer.</summary>
    public PanelElement()
    {
        _renderer = new NinePatchRenderer
        {
            IsVisible = false,
            BorderScale = 0.4f,
            RenderLayerMask = RenderLayers.DefaultUI,
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

    /// <summary>Synchronizes renderer state when this element or an ancestor changes.</summary>
    /// <param name="propertyName">The name of the changed property.</param>
    private void OnLayoutChanged(string propertyName)
    {
        if (propertyName is "" or nameof(Bounds) or nameof(IsVisible) or nameof(SortOrder))
            SynchronizeRenderer();
    }

    /// <summary>Gets normalized texture coordinates for the configured atlas region.</summary>
    /// <param name="texture">The texture whose region coordinates are requested.</param>
    /// <returns>The configured region, or the full texture when no region is configured.</returns>
    private Vector4D<float> GetTexCoord(ITexture? texture)
    {
        if (texture is null || _regionName is null)
            return new(0f, 0f, 1f, 1f);
        var uv = texture.GetRegion(_regionName).TexCoords;
        return new(uv.Origin.X, uv.Origin.Y, uv.Size.X, uv.Size.Y);
    }

    /// <summary>Updates the owned renderer from the panel's current properties and layout.</summary>
    private void SynchronizeRenderer()
    {
        var visible = Bounds.Size.X > 0f && Bounds.Size.Y > 0f;
        for (ISceneNode? node = this; node is not null; node = node.Parent)
            if (node is IElement element && !element.IsVisible)
                visible = false;
        _renderer.IsVisible = false;
        _renderer.DrawOrder = SortOrder;
        var atlas = _isSelected ? _selectedTexture ?? _texture : _texture;
        var uv = GetTexCoord(atlas);
        _renderer.Texture = atlas;
        _renderer.TexCoord = uv;
        _renderer.SourceBorders = _sourceBorders;
        _renderer.Destination = Bounds;
        _renderer.IsVisible = visible;
    }
}
