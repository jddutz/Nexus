namespace Nexus.GUI.Elements;

/// <summary>Lays out, clips, and renders one texture image inside its assigned rectangle.</summary>
public partial class ImageElement : Element
{
    private readonly List<IObservable> _visibilityAncestors = [];
    private Texture? _texture;
    private Rectangle<int>? _sourceRegion;

    /// <summary>Gets or sets the texture containing the image.</summary>
    public Texture? Texture
    {
        get => _texture;
        set
        {
            if (EqualityComparer<Texture?>.Default.Equals(_texture, value))
                return;
            if (value is not null)
                ValidateTexture(value, _sourceRegion);

            var previousValue = _texture;
            _texture = value;
            UpdateTextureSource();
            NotifyPropertyChanged(nameof(Texture));
            if ((previousValue is null) != (value is null) && IsEffectivelyVisible)
                InvalidateLayout();
            TextureChanged?.Invoke(previousValue, value);
        }
    }

    /// <summary>Occurs when the texture changes.</summary>
    public event Action<Texture?, Texture?>? TextureChanged;

    /// <summary>Gets or sets the source pixel rectangle, or null for the full texture.</summary>
    public Rectangle<int>? SourceRegion
    {
        get => _sourceRegion;
        set
        {
            if (EqualityComparer<Rectangle<int>?>.Default.Equals(_sourceRegion, value))
                return;
            if (value is { } sourceRegion)
                ValidateSourceRegion(sourceRegion, _texture);

            var previousValue = _sourceRegion;
            _sourceRegion = value;
            UpdateTextureSource();
            NotifyPropertyChanged(nameof(SourceRegion));
            if (IsEffectivelyVisible)
                InvalidateLayout();
            SourceRegionChanged?.Invoke(previousValue, value);
        }
    }

    /// <summary>Occurs when the source region changes.</summary>
    public event Action<Rectangle<int>?, Rectangle<int>?>? SourceRegionChanged;

    [Observable(PublicSetter = true)]
    private SizingMode _sizingMode = SizingMode.Original;

    [Observable]
    private AlignHorizontal _horizontalAlignment = AlignHorizontal.Center;

    [Observable]
    private AlignVertical _verticalAlignment = AlignVertical.Center;
    private Vector2D<float>? _customSize;
    private Vector4D<float>? _customTexCoord;

    [Observable]
    private ISamplingBehavior _samplingBehavior = SamplingBehaviors.Smooth;

    [Observable]
    private Color _color = Colors.White;

    [Observable]
    private ulong _renderLayerMask = ulong.MaxValue;
    private TextureComponent? _imageComponent;

    /// <summary>Validates the proposed sizing mode and custom-size configuration.</summary>
    /// <param name="value">The proposed sizing mode.</param>
    private void BeforeSizingModeChanges(SizingMode value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value));
        if (value == SizingMode.Custom && (_customSize is null || _customTexCoord is null))
            throw new InvalidOperationException(
                "Use SetCustomSizingMode to provide a custom size and UV rectangle."
            );
    }

    /// <summary>Validates the proposed horizontal alignment.</summary>
    /// <param name="value">The proposed horizontal alignment.</param>
    private void BeforeHorizontalAlignmentChanges(AlignHorizontal value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>Validates the proposed vertical alignment.</summary>
    /// <param name="value">The proposed vertical alignment.</param>
    private void BeforeVerticalAlignmentChanges(AlignVertical value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>Rejects a null sampling behavior.</summary>
    /// <param name="value">The proposed sampling behavior.</param>
    private void BeforeSamplingBehaviorChanges(ISamplingBehavior value) =>
        ArgumentNullException.ThrowIfNull(value);

    /// <summary>Updates texture sampling after its behavior changes.</summary>
    /// <param name="previousValue">The previous sampling behavior.</param>
    protected virtual partial void AfterSamplingBehaviorChanges(ISamplingBehavior previousValue)
    {
        if (_imageComponent is not null)
            _imageComponent.SamplingBehavior = SamplingBehavior;
    }

    /// <summary>Updates the image component after its color changes.</summary>
    /// <param name="previousValue">The previous color.</param>
    protected virtual partial void AfterColorChanges(Color previousValue)
    {
        if (_imageComponent is not null)
            _imageComponent.Color = Color;
    }

    /// <summary>Updates the image component after its render-layer mask changes.</summary>
    /// <param name="previousValue">The previous render-layer mask.</param>
    protected virtual partial void AfterRenderLayerMaskChanges(ulong previousValue)
    {
        if (_imageComponent is not null)
            _imageComponent.RenderLayerMask = RenderLayerMask;
    }

    /// <summary>Gets the custom image size, when configured through <see cref="SetCustomSizingMode" />.</summary>
    public Vector2D<float>? CustomSize => _customSize;

    /// <summary>Gets the custom normalized source rectangle used by custom sizing.</summary>
    public Vector4D<float>? CustomTexCoord => _customTexCoord;

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
            NotifyPropertyChanged(nameof(CustomSize));
            configurationChanged = true;
        }
        if (_customTexCoord != customTexCoord)
        {
            _customTexCoord = customTexCoord;
            NotifyPropertyChanged(nameof(CustomTexCoord));
            configurationChanged = true;
        }

        if (SizingMode != SizingMode.Custom)
            SetSizingMode(SizingMode.Custom);
        if (configurationChanged)
            UpdateTextureSource();
    }

    /// <inheritdoc />
    public override Vector2D<float> Measure(Vector2D<float> constraint)
    {
        ValidateAvailableSize(constraint, nameof(constraint));
        var texture = Texture;
        if (
            !IsEffectivelyVisible
            || texture is null
            || constraint.X == 0f
            || constraint.Y == 0f
        )
            return Vector2D<float>.Zero;

        var imageSize = GetImageSize(GetEffectiveSourceRegion(texture), constraint);
        return new(MathF.Min(imageSize.X, constraint.X), MathF.Min(imageSize.Y, constraint.Y));
    }

    /// <inheritdoc />
    public override void Arrange(Rectangle<float> bounds)
    {
        ValidateBounds(bounds);
        base.Arrange(bounds);
        UpdateVisualComponent(bounds);
    }

    /// <summary>Synchronizes the image drawable and hit bounds with the current allocation.</summary>
    /// <param name="bounds">The allocation supplied by the layout pass.</param>
    private void UpdateVisualComponent(Rectangle<float> bounds)
    {
        var texture = Texture;
        if (!IsEffectivelyVisible || texture is null)
        {
            RemoveVisualComponent();
            SetBounds(new Rectangle<float>(bounds.Origin, Vector2D<float>.Zero));
            return;
        }

        if (bounds.Size.X <= 0f || bounds.Size.Y <= 0f)
        {
            RemoveVisualComponent();
            SetBounds(EmptyBounds);
            return;
        }

        var sourceRegion = GetEffectiveSourceRegion(texture);
        var imageSize = GetImageSize(sourceRegion, bounds.Size);
        var imageX = GetHorizontalOffset(bounds.Size.X, imageSize.X);
        var imageY = GetVerticalOffset(bounds.Size.Y, imageSize.Y);
        var left = MathF.Max(0f, imageX);
        var top = MathF.Max(0f, imageY);
        var right = MathF.Min(bounds.Size.X, imageX + imageSize.X);
        var bottom = MathF.Min(bounds.Size.Y, imageY + imageSize.Y);
        if (right <= left || bottom <= top)
        {
            RemoveVisualComponent();
            SetBounds(EmptyBounds);
            return;
        }

        var sourceTexCoord = GetActiveTexCoord(texture, sourceRegion);
        var leftFraction = (left - imageX) / imageSize.X;
        var topFraction = (top - imageY) / imageSize.Y;
        var rightFraction = (right - imageX) / imageSize.X;
        var bottomFraction = (bottom - imageY) / imageSize.Y;
        var texCoord = new Vector4D<float>(
            sourceTexCoord.X + leftFraction * sourceTexCoord.Z,
            sourceTexCoord.Y + topFraction * sourceTexCoord.W,
            (rightFraction - leftFraction) * sourceTexCoord.Z,
            (bottomFraction - topFraction) * sourceTexCoord.W
        );

        SetBounds(
            new Rectangle<float>(
                bounds.Origin.X + left,
                bounds.Origin.Y + top,
                right - left,
                bottom - top
            )
        );
        if (_imageComponent is null)
        {
            _imageComponent = new TextureComponent();
            AddComponent(_imageComponent);
        }

        SynchronizeVisualComponent(
            _imageComponent,
            texture,
            new Rectangle<float>(
                bounds.Origin.X + left,
                bounds.Origin.Y + top,
                right - left,
                bottom - top
            ),
            texCoord
        );
    }

    /// <summary>Updates the active drawable's texture and source coordinates without relayout.</summary>
    private void UpdateTextureSource()
    {
        if (_imageComponent is null)
            return;

        if (Texture is not { } texture)
        {
            RemoveVisualComponent();
            SetBounds(new Rectangle<float>(Bounds.Origin, Vector2D<float>.Zero));
            return;
        }

        _imageComponent.Texture = texture;
        _imageComponent.TexCoord = GetActiveTexCoord(texture, GetEffectiveSourceRegion(texture));
    }

    /// <summary>Applies retained source and visual settings to the current texture component.</summary>
    /// <param name="component">The owned visual component.</param>
    /// <param name="destination">The clipped visual destination.</param>
    /// <param name="texCoord">The clipped normalized source rectangle.</param>
    private void SynchronizeVisualComponent(
        TextureComponent component,
        Texture texture,
        Rectangle<float> destination,
        Vector4D<float> texCoord
    )
    {
        component.Texture = texture;
        component.Destination = destination;
        component.TexCoord = texCoord;
        component.Color = Color;
        component.SamplingBehavior = SamplingBehavior;
        component.RenderLayerMask = RenderLayerMask;
    }

    /// <summary>Removes the current visual component while retaining image configuration.</summary>
    private void RemoveVisualComponent()
    {
        var component = _imageComponent;
        _imageComponent = null;
        if (component is not null)
            RemoveComponent(component);
    }

    /// <summary>Calculates the uncapped image size for the selected sizing mode.</summary>
    /// <param name="availableSize">The assigned rectangle or measure constraint.</param>
    /// <returns>The image size before clipping.</returns>
    private Vector2D<float> GetImageSize(Rectangle<int> sourceRegion, Vector2D<float> availableSize)
    {
        var sourceWidth = sourceRegion.Size.X;
        var sourceHeight = sourceRegion.Size.Y;
        var sourceSize = new Vector2D<float>(sourceWidth, sourceHeight);
        var imageSize = SizingMode switch
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

    /// <summary>Gets the horizontal offset for the selected image alignment.</summary>
    /// <param name="availableSize">The assigned rectangle width.</param>
    /// <param name="imageSize">The rendered image width before clipping.</param>
    /// <returns>The image's left edge in element-local coordinates.</returns>
    private float GetHorizontalOffset(float availableSize, float imageSize) =>
        HorizontalAlignment switch
        {
            AlignHorizontal.Left => 0f,
            AlignHorizontal.Center => (availableSize - imageSize) / 2f,
            AlignHorizontal.Right => availableSize - imageSize,
            _ => throw new InvalidOperationException(),
        };

    /// <summary>Gets the vertical offset for the selected image alignment.</summary>
    /// <param name="availableSize">The assigned rectangle height.</param>
    /// <param name="imageSize">The rendered image height before clipping.</param>
    /// <returns>The image's top edge in element-local coordinates.</returns>
    private float GetVerticalOffset(float availableSize, float imageSize) =>
        VerticalAlignment switch
        {
            AlignVertical.Top => 0f,
            AlignVertical.Center => (availableSize - imageSize) / 2f,
            AlignVertical.Bottom => availableSize - imageSize,
            _ => throw new InvalidOperationException(),
        };

    /// <summary>Gets the currently selected normalized source rectangle.</summary>
    /// <returns>The custom UV rectangle in custom mode, or the source pixel rectangle normalized.</returns>
    private Vector4D<float> GetActiveTexCoord(Texture texture, Rectangle<int> region)
    {
        if (SizingMode == SizingMode.Custom)
            return _customTexCoord!.Value;

        return new(
            (float)region.Origin.X / texture.Width,
            (float)region.Origin.Y / texture.Height,
            (float)region.Size.X / texture.Width,
            (float)region.Size.Y / texture.Height
        );
    }

    /// <summary>Gets the configured source rectangle or the full texture when omitted.</summary>
    /// <param name="texture">The texture providing default dimensions.</param>
    /// <returns>The effective pixel rectangle.</returns>
    private Rectangle<int> GetEffectiveSourceRegion(Texture texture) =>
        SourceRegion ?? new Rectangle<int>(0, 0, (int)texture.Width, (int)texture.Height);

    /// <summary>Validates texture dimensions and that its source region fits within it.</summary>
    /// <param name="texture">The texture to validate.</param>
    /// <param name="sourceRegion">The optional source pixel rectangle.</param>
    private static void ValidateTexture(Texture texture, Rectangle<int>? sourceRegion)
    {
        if (
            texture.Width == 0
            || texture.Height == 0
            || texture.Width > int.MaxValue
            || texture.Height > int.MaxValue
        )
            throw new ArgumentOutOfRangeException(
                nameof(texture),
                "Texture dimensions must be positive and representable as pixel coordinates."
            );

        if (sourceRegion is { } region)
            ValidateSourceRegion(region, texture);
    }

    /// <summary>Validates that a source rectangle is positive and contained by its texture.</summary>
    /// <param name="sourceRegion">The proposed source rectangle.</param>
    /// <param name="texture">The texture, when available for containment validation.</param>
    private static void ValidateSourceRegion(Rectangle<int> sourceRegion, Texture? texture)
    {
        var origin = sourceRegion.Origin;
        var size = sourceRegion.Size;
        if (
            origin.X < 0
            || origin.Y < 0
            || size.X <= 0
            || size.Y <= 0
            || (
                texture is not null
                && (
                    (long)origin.X + size.X > texture.Width
                    || (long)origin.Y + size.Y > texture.Height
                )
            )
        )
            throw new ArgumentOutOfRangeException(
                nameof(sourceRegion),
                "The source rectangle must have positive dimensions and fit inside the texture."
            );
    }

    /// <summary>Subscribes to visibility changes on the current ancestor chain.</summary>
    private void UpdateVisibilityAncestorSubscriptions()
    {
        foreach (var ancestor in _visibilityAncestors)
            ancestor.PropertyChanged -= OnAncestorPropertyChanged;
        _visibilityAncestors.Clear();

        for (ISceneNode? ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor is not IObservable observable)
                continue;

            observable.PropertyChanged += OnAncestorPropertyChanged;
            _visibilityAncestors.Add(observable);
        }
    }

    /// <summary>Recreates the image visual when an ancestor's visibility changes.</summary>
    /// <param name="propertyName">The name of the changed property.</param>
    private void OnAncestorPropertyChanged(string propertyName)
    {
        if (propertyName is "" or nameof(IsVisible))
            ClearVisualWhenHidden();
    }

    /// <inheritdoc />
    public override void OnSceneHierarchyChanged()
    {
        base.OnSceneHierarchyChanged();
        UpdateVisibilityAncestorSubscriptions();
        ClearVisualWhenHidden();
    }

    /// <inheritdoc />
    protected override void AfterIsVisibleChanges()
    {
        base.AfterIsVisibleChanges();
        ClearVisualWhenHidden();
    }

    /// <summary>Removes image visuals and clears hit bounds when this element is hidden.</summary>
    private void ClearVisualWhenHidden()
    {
        if (IsEffectivelyVisible)
            return;

        RemoveVisualComponent();
        SetBounds(new Rectangle<float>(Bounds.Origin, Vector2D<float>.Zero));
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
