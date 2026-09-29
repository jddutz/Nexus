namespace Nexus.GUI.Elements;

/// <summary>Lays out, clips, and renders one texture image inside its assigned rectangle.</summary>
public sealed class ImageElement : Element
{
    private const int MaximumImageQuadCount = 1_000_000;

    private readonly List<IGameObject> _visibilityAncestors = [];
    private ImageSource _imageSource;
    private SizingMode _sizingMode;
    private TileMode _tileMode;
    private AlignHorizontal _horizontalAlignment = AlignHorizontal.Center;
    private AlignVertical _verticalAlignment = AlignVertical.Center;
    private Vector2D<float>? _customSize;
    private Vector4D<float>? _customTexCoord;
    private ISamplingBehavior _samplingBehavior = SamplingBehaviors.Smooth;
    private Color _color = Colors.White;
    private ulong _renderLayerMask = ulong.MaxValue;
    private TextureComponent? _imageComponent;
    private Rectangle<float>? _layoutBounds;
    private Rectangle<float>? _cachedLayoutBounds;
    private ImageInstanceLayout _cachedLayout = ImageInstanceLayout.Empty;
    private bool _geometryDirty = true;

    /// <summary>Gets or sets the texture and pixel-space image region used by this element.</summary>
    public ImageSource ImageSource
    {
        get => _imageSource;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetProperty(ref _imageSource, value))
                InvalidateGeometry();
        }
    }

    /// <summary>Gets or sets the sizing rule used to determine the image's tile size.</summary>
    public SizingMode SizingMode
    {
        get => _sizingMode;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (value == SizingMode.Custom && (_customSize is null || _customTexCoord is null))
                throw new InvalidOperationException(
                    "Use SetCustomSizingMode to provide a custom size and UV rectangle."
                );

            if (SetProperty(ref _sizingMode, value))
                InvalidateGeometry();
        }
    }

    /// <summary>Gets the custom image size, when configured through <see cref="SetCustomSizingMode" />.</summary>
    public Vector2D<float>? CustomSize => _customSize;

    /// <summary>Gets the custom normalized source rectangle used by custom sizing.</summary>
    public Vector4D<float>? CustomTexCoord => _customTexCoord;

    /// <summary>Gets or sets which axes repeat the image tile.</summary>
    public TileMode TileMode
    {
        get => _tileMode;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (SetProperty(ref _tileMode, value))
                InvalidateGeometry();
        }
    }

    /// <summary>Gets or sets horizontal placement and repeat-pattern anchoring.</summary>
    public AlignHorizontal HorizontalAlignment
    {
        get => _horizontalAlignment;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (SetProperty(ref _horizontalAlignment, value))
                InvalidateGeometry();
        }
    }

    /// <summary>Gets or sets vertical placement and repeat-pattern anchoring.</summary>
    public AlignVertical VerticalAlignment
    {
        get => _verticalAlignment;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (SetProperty(ref _verticalAlignment, value))
                InvalidateGeometry();
        }
    }

    /// <summary>Gets or sets the texture sampling behavior used by the visual component.</summary>
    public ISamplingBehavior SamplingBehavior
    {
        get => _samplingBehavior;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetProperty(ref _samplingBehavior, value) && _imageComponent is not null)
                _imageComponent.SamplingBehavior = value;
        }
    }

    /// <summary>Gets or sets the tint multiplied against sampled source pixels.</summary>
    public Color Color
    {
        get => _color;
        set
        {
            if (SetProperty(ref _color, value) && _imageComponent is not null)
                _imageComponent.Color = value;
        }
    }

    /// <summary>Gets or sets the render-layer mask applied to the visual component.</summary>
    public ulong RenderLayerMask
    {
        get => _renderLayerMask;
        set
        {
            if (SetProperty(ref _renderLayerMask, value) && _imageComponent is not null)
                _imageComponent.RenderLayerMask = value;
        }
    }

    /// <summary>Initializes an image element with a required texture source.</summary>
    /// <param name="imageSource">The texture and optional pixel-space source rectangle.</param>
    public ImageElement(ImageSource imageSource)
        : base()
    {
        ArgumentNullException.ThrowIfNull(imageSource);
        _imageSource = imageSource;
        UpdateVisualComponent();
    }

    /// <summary>Sets custom sizing and its independent normalized source UV rectangle atomically.</summary>
    /// <param name="customSize">The positive destination tile size in logical units.</param>
    /// <param name="customTexCoord">The positive normalized source rectangle within the texture.</param>
    public void SetCustomSizingMode(Vector2D<float> customSize, Vector4D<float> customTexCoord)
    {
        ValidatePositiveSize(customSize, nameof(customSize));
        ValidateNormalizedTexCoord(customTexCoord, nameof(customTexCoord));

        var configurationChanged = false;
        if (_customSize != customSize)
        {
            _customSize = customSize;
            OnPropertyChanged(nameof(CustomSize));
            configurationChanged = true;
        }
        if (_customTexCoord != customTexCoord)
        {
            _customTexCoord = customTexCoord;
            OnPropertyChanged(nameof(CustomTexCoord));
            configurationChanged = true;
        }

        if (_sizingMode != SizingMode.Custom)
            SizingMode = SizingMode.Custom;
        else if (configurationChanged)
            InvalidateGeometry();
    }

    /// <inheritdoc />
    public override Vector2D<float> Measure(Vector2D<float> constraint)
    {
        ValidateAvailableSize(constraint, nameof(constraint));
        if (!IsEffectivelyVisible || constraint.X == 0f || constraint.Y == 0f)
            return Vector2D<float>.Zero;

        var imageSize = GetImageSize(constraint);
        return new(MathF.Min(imageSize.X, constraint.X), MathF.Min(imageSize.Y, constraint.Y));
    }

    /// <inheritdoc />
    public override void Arrange(Rectangle<float> bounds)
    {
        ValidateBounds(bounds);
        _layoutBounds = bounds;
        base.Arrange(bounds);
        UpdateVisualComponent();
    }

    /// <summary>Invalidates cached quads and reapplies the last parent-assigned rectangle.</summary>
    private void InvalidateGeometry()
    {
        _geometryDirty = true;
        if (_layoutBounds is { } bounds)
            Arrange(bounds);
    }

    /// <summary>Synchronizes visual-component ownership, source data, and actual hit bounds.</summary>
    private void UpdateVisualComponent()
    {
        if (!IsEffectivelyVisible || _layoutBounds is not { } bounds)
        {
            RemoveVisualComponent();
            Bounds = EmptyBounds;
            return;
        }

        var layout = GetImageLayout(bounds);
        if (layout.InstanceCount == 0)
        {
            RemoveVisualComponent();
            Bounds = EmptyBounds;
            return;
        }

        Position = bounds.Origin;
        Bounds = layout.GetBounds(bounds.Origin);
        if (_imageComponent is null)
        {
            _imageComponent = new TextureComponent();
            AddComponent(_imageComponent);
        }

        SynchronizeVisualComponent(_imageComponent, bounds, layout);
    }

    /// <summary>Applies retained source and visual settings to the current texture component.</summary>
    /// <param name="component">The owned visual component.</param>
    /// <param name="bounds">The assigned layout rectangle.</param>
    /// <param name="quads">The arranged image quads.</param>
    private void SynchronizeVisualComponent(
        TextureComponent component,
        Rectangle<float> bounds,
        ImageInstanceLayout layout
    )
    {
        component.Texture = _imageSource.Texture;
        component.TexCoord = GetActiveTexCoord();
        component.Size = GetImageSize(bounds.Size);
        component.Color = _color;
        component.SamplingBehavior = _samplingBehavior;
        component.RenderLayerMask = _renderLayerMask;
        component.SetInstanceGeometry(layout.InstanceCount, layout.GetInstance);
    }

    /// <summary>Removes the current visual component while retaining image configuration.</summary>
    private void RemoveVisualComponent()
    {
        var component = _imageComponent;
        _imageComponent = null;
        if (component is not null)
            RemoveComponent(component);
    }

    /// <summary>Gets the cached or newly arranged indexed instance layout.</summary>
    /// <param name="bounds">The parent-assigned rectangle.</param>
    /// <returns>The clipped indexed image instance layout.</returns>
    private ImageInstanceLayout GetImageLayout(Rectangle<float> bounds)
    {
        if (!_geometryDirty && _cachedLayoutBounds == bounds)
            return _cachedLayout;

        _cachedLayout = CreateImageLayout(bounds.Size);
        _cachedLayoutBounds = bounds;
        _geometryDirty = false;
        return _cachedLayout;
    }

    /// <summary>Creates an indexed layout for the current sizing, alignment, and tiling settings.</summary>
    /// <param name="availableSize">The assigned rectangle's size.</param>
    /// <returns>The clipped indexed image layout.</returns>
    private ImageInstanceLayout CreateImageLayout(Vector2D<float> availableSize)
    {
        if (availableSize.X <= 0f || availableSize.Y <= 0f)
            return ImageInstanceLayout.Empty;

        var imageSize = GetImageSize(availableSize);
        var tileHorizontally = _tileMode is TileMode.Horizontal or TileMode.Both;
        var tileVertically = _tileMode is TileMode.Vertical or TileMode.Both;
        var xPositions = GetTilePositions(
            availableSize.X,
            imageSize.X,
            tileHorizontally,
            _horizontalAlignment == AlignHorizontal.Left,
            _horizontalAlignment == AlignHorizontal.Right
        );
        var yPositions = GetTilePositions(
            availableSize.Y,
            imageSize.Y,
            tileVertically,
            _verticalAlignment == AlignVertical.Top,
            _verticalAlignment == AlignVertical.Bottom
        );
        var instanceCount = checked((long)xPositions.Count * yPositions.Count);
        if (instanceCount > MaximumImageQuadCount)
            throw new InvalidOperationException(
                $"The image arrangement exceeds the maximum of {MaximumImageQuadCount} instances."
            );

        return new ImageInstanceLayout(
            availableSize,
            imageSize,
            xPositions,
            yPositions,
            GetActiveTexCoord()
        );
    }

    /// <summary>Computes tile origins using the selected alignment as a repeat-pattern anchor.</summary>
    /// <param name="available">The available axis extent.</param>
    /// <param name="tileSize">The positive image tile extent.</param>
    /// <param name="tiled">Whether to repeat along this axis.</param>
    /// <param name="leading">Whether to anchor at the leading edge.</param>
    /// <param name="trailing">Whether to anchor at the trailing edge.</param>
    /// <returns>The tile origins that intersect the available axis.</returns>
    private static List<double> GetTilePositions(
        float available,
        float tileSize,
        bool tiled,
        bool leading,
        bool trailing
    )
    {
        if (!tiled)
            return
            [
                leading ? 0d
                : trailing ? available - tileSize
                : (available - tileSize) / 2d,
            ];

        var tileRatio = (double)available / tileSize;
        if (!double.IsFinite(tileRatio))
            throw new InvalidOperationException("The image tile count is not finite.");
        var tileCount = Math.Max(1L, checked((long)Math.Ceiling(tileRatio)));
        if (tileCount > MaximumImageQuadCount)
            throw new InvalidOperationException(
                $"The image arrangement exceeds the maximum of {MaximumImageQuadCount} tiles on an axis."
            );

        var positions = new List<double>();
        if (leading)
        {
            for (long index = 0; index < tileCount; index++)
                positions.Add(index * (double)tileSize);
        }
        else if (trailing)
        {
            for (long index = 0; index < tileCount; index++)
                positions.Add(available - tileSize - index * (double)tileSize);
        }
        else
        {
            var centerTile = (available - tileSize) / 2d;
            for (var index = -tileCount; index <= tileCount; index++)
            {
                var position = centerTile + index * (double)tileSize;
                if (position < available && position + tileSize > 0d)
                    positions.Add(position);
            }
        }

        if (positions.Count > MaximumImageQuadCount)
            throw new InvalidOperationException(
                $"The image arrangement exceeds the maximum of {MaximumImageQuadCount} tiles on an axis."
            );
        return positions;
    }

    /// <summary>Calculates the uncapped image size for the selected sizing mode.</summary>
    /// <param name="availableSize">The assigned rectangle or measure constraint.</param>
    /// <returns>The image size before clipping.</returns>
    private Vector2D<float> GetImageSize(Vector2D<float> availableSize)
    {
        var sourceWidth = _imageSource.SourceRegion.Size.X;
        var sourceHeight = _imageSource.SourceRegion.Size.Y;
        var sourceSize = new Vector2D<float>(sourceWidth, sourceHeight);
        var imageSize = _sizingMode switch
        {
            SizingMode.Original => sourceSize,
            SizingMode.Fit => sourceSize
                * MathF.Min(availableSize.X / sourceSize.X, availableSize.Y / sourceSize.Y),
            SizingMode.FitHorizontal => new(
                availableSize.X,
                sourceSize.Y * availableSize.X / sourceSize.X
            ),
            SizingMode.FitVertical => new(
                sourceSize.X * availableSize.Y / sourceSize.Y,
                availableSize.Y
            ),
            SizingMode.Fill => sourceSize
                * MathF.Max(availableSize.X / sourceSize.X, availableSize.Y / sourceSize.Y),
            SizingMode.Stretch => availableSize,
            SizingMode.Custom => _customSize!.Value,
            _ => throw new InvalidOperationException("The configured sizing mode is invalid."),
        };
        if (
            !float.IsFinite(imageSize.X)
            || !float.IsFinite(imageSize.Y)
            || imageSize.X <= 0f
            || imageSize.Y <= 0f
        )
            throw new InvalidOperationException(
                "The image sizing calculation was not positive and finite."
            );

        return imageSize;
    }

    /// <summary>Gets the currently selected normalized source rectangle.</summary>
    /// <returns>The custom UV rectangle in custom mode, or the source pixel rectangle normalized.</returns>
    private Vector4D<float> GetActiveTexCoord()
    {
        if (_sizingMode == SizingMode.Custom)
            return _customTexCoord!.Value;

        var region = _imageSource.SourceRegion;
        return new(
            (float)region.Origin.X / _imageSource.Texture.Width,
            (float)region.Origin.Y / _imageSource.Texture.Height,
            (float)region.Size.X / _imageSource.Texture.Width,
            (float)region.Size.Y / _imageSource.Texture.Height
        );
    }

    /// <summary>Subscribes to visibility changes on the current ancestor chain.</summary>
    private void UpdateVisibilityAncestorSubscriptions()
    {
        foreach (var ancestor in _visibilityAncestors)
            ancestor.PropertyChanged -= OnAncestorPropertyChanged;
        _visibilityAncestors.Clear();

        for (var ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            ancestor.PropertyChanged += OnAncestorPropertyChanged;
            _visibilityAncestors.Add(ancestor);
        }
    }

    /// <summary>Recreates the image visual when an ancestor's visibility changes.</summary>
    /// <param name="sender">The ancestor that changed.</param>
    /// <param name="eventArgs">The property-change details.</param>
    private void OnAncestorPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is null or nameof(IsVisible))
            UpdateVisualComponent();
    }

    /// <inheritdoc />
    protected override void OnHierarchyChanged()
    {
        base.OnHierarchyChanged();
        UpdateVisibilityAncestorSubscriptions();
        UpdateVisualComponent();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsVisible))
            UpdateVisualComponent();
    }

    /// <summary>Validates a positive finite logical size.</summary>
    /// <param name="size">The proposed size.</param>
    /// <param name="parameterName">The argument name used for validation errors.</param>
    private static void ValidatePositiveSize(Vector2D<float> size, string parameterName)
    {
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X <= 0f || size.Y <= 0f)
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Both size dimensions must be positive and finite."
            );
    }

    /// <summary>Validates a positive normalized UV rectangle contained in the texture.</summary>
    /// <param name="texCoord">The proposed UV origin and extent.</param>
    /// <param name="parameterName">The argument name used for validation errors.</param>
    private static void ValidateNormalizedTexCoord(Vector4D<float> texCoord, string parameterName)
    {
        if (
            !float.IsFinite(texCoord.X)
            || !float.IsFinite(texCoord.Y)
            || !float.IsFinite(texCoord.Z)
            || !float.IsFinite(texCoord.W)
            || texCoord.X < 0f
            || texCoord.Y < 0f
            || texCoord.Z <= 0f
            || texCoord.W <= 0f
            || texCoord.X + texCoord.Z > 1f
            || texCoord.Y + texCoord.W > 1f
        )
            throw new ArgumentOutOfRangeException(
                parameterName,
                "The UV rectangle must have positive dimensions and fit within [0, 1]."
            );
    }

    /// <summary>Validates a finite, non-negative available size.</summary>
    /// <param name="size">The available size.</param>
    /// <param name="parameterName">The argument name used for validation errors.</param>
    private static void ValidateAvailableSize(Vector2D<float> size, string parameterName)
    {
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X < 0f || size.Y < 0f)
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Available dimensions must be finite and non-negative."
            );
    }

    /// <summary>Validates the origin and non-negative finite extents of an assigned rectangle.</summary>
    /// <param name="bounds">The proposed arranged rectangle.</param>
    private static void ValidateBounds(Rectangle<float> bounds)
    {
        ValidateAvailableSize(bounds.Size, nameof(bounds));
        if (
            !float.IsFinite(bounds.Origin.X)
            || !float.IsFinite(bounds.Origin.Y)
            || !float.IsFinite(bounds.Max.X)
            || !float.IsFinite(bounds.Max.Y)
        )
            throw new ArgumentOutOfRangeException(
                nameof(bounds),
                "The arranged rectangle must have finite coordinates."
            );
    }

    /// <summary>Gets the empty rectangle used when no image pixels are arranged.</summary>
    private static Rectangle<float> EmptyBounds => new(0f, 0f, 0f, 0f);
}
